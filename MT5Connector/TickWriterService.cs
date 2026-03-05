using System.Threading.Channels;
using Npgsql;
using NpgsqlTypes;

namespace MT5Connector
{
    public class TickWriterService
    {
        private readonly DbConfig _dbConfig;
        private readonly Channel<TickData> _channel;
        private Task? _writerTask;
        private CancellationTokenSource? _cts;
        private readonly HashSet<string> _createdPartitions = new();

        private const int BatchSize = 2000;
        private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(1);

        public TickWriterService(DbConfig dbConfig)
        {
            _dbConfig = dbConfig;
            _channel = Channel.CreateBounded<TickData>(50000);
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            // Ensure partitions for today and tomorrow on startup, then drop old ones
            _ = Task.Run(async () =>
            {
                try
                {
                    var today = DateTime.UtcNow.Date;
                    await EnsurePartition(today);
                    await EnsurePartition(today.AddDays(1));
                    await DropOldPartitions();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TickWriter] Startup partition setup error: {ex.Message}");
                }
            });

            _writerTask = Task.Run(async () => await WriterLoop(token), token);
            Console.WriteLine("[TickWriter] Started");
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        public async ValueTask EnqueueTick(TickData tick)
        {
            await _channel.Writer.WriteAsync(tick);
        }

        public async Task EnsurePartition(DateTime date)
        {
            var partitionName = $"tick_history_{date:yyyyMMdd}";

            if (_createdPartitions.Contains(partitionName))
                return;

            try
            {
                await using var conn = new NpgsqlConnection(_dbConfig.ConnectionString);
                await conn.OpenAsync();

                var startDate = date.ToString("yyyy-MM-dd");
                var endDate = date.AddDays(1).ToString("yyyy-MM-dd");

                // Check if partition already exists
                var exists = await ExecuteScalarAsync<bool>(conn,
                    "SELECT EXISTS (SELECT 1 FROM pg_class WHERE relname = @partitionName)",
                    new NpgsqlParameter("partitionName", partitionName));

                if (!exists)
                {
                    await using var cmd = new NpgsqlCommand($@"
                        CREATE TABLE {partitionName} PARTITION OF tick_history
                        FOR VALUES FROM ('{startDate}') TO ('{endDate}')", conn);
                    await cmd.ExecuteNonQueryAsync();

                    await using var idxCmd = new NpgsqlCommand($@"
                        CREATE INDEX IF NOT EXISTS idx_{partitionName}_symbol_time
                        ON {partitionName}(symbol, tick_time DESC)", conn);
                    await idxCmd.ExecuteNonQueryAsync();

                    Console.WriteLine($"[TickWriter] Created partition {partitionName}");
                }

                _createdPartitions.Add(partitionName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TickWriter] EnsurePartition error for {partitionName}: {ex.Message}");
            }
        }

        private async Task DropOldPartitions()
        {
            try
            {
                await using var conn = new NpgsqlConnection(_dbConfig.ConnectionString);
                await conn.OpenAsync();

                var cutoffDate = DateTime.UtcNow.Date.AddDays(-30);

                // Find all tick_history partitions
                await using var cmd = new NpgsqlCommand(@"
                    SELECT relname FROM pg_class
                    WHERE relname LIKE 'tick_history_%' AND relkind = 'r'
                    ORDER BY relname", conn);

                var partitionsToDrop = new List<string>();
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var name = reader.GetString(0);
                    // Parse date from partition name: tick_history_YYYYMMDD
                    if (name.Length == 23 && // "tick_history_" (13) + "YYYYMMDD" (8) + possible extra
                        DateTime.TryParseExact(name.Substring(13, 8), "yyyyMMdd",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out var partDate))
                    {
                        if (partDate < cutoffDate)
                        {
                            partitionsToDrop.Add(name);
                        }
                    }
                }

                foreach (var partition in partitionsToDrop)
                {
                    try
                    {
                        await using var dropConn = new NpgsqlConnection(_dbConfig.ConnectionString);
                        await dropConn.OpenAsync();
                        await using var dropCmd = new NpgsqlCommand($"DROP TABLE IF EXISTS {partition}", dropConn);
                        await dropCmd.ExecuteNonQueryAsync();
                        Console.WriteLine($"[TickWriter] Dropped old partition {partition}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[TickWriter] Error dropping partition {partition}: {ex.Message}");
                    }
                }

                if (partitionsToDrop.Count > 0)
                    Console.WriteLine($"[TickWriter] Dropped {partitionsToDrop.Count} old partitions (>30 days)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TickWriter] DropOldPartitions error: {ex.Message}");
            }
        }

        private async Task WriterLoop(CancellationToken ct)
        {
            var buffer = new List<TickData>(BatchSize);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    buffer.Clear();

                    // Wait for first tick or cancellation
                    if (await _channel.Reader.WaitToReadAsync(ct))
                    {
                        // Drain channel up to batch size, with flush timeout
                        var flushDeadline = DateTime.UtcNow.Add(FlushInterval);

                        while (buffer.Count < BatchSize && DateTime.UtcNow < flushDeadline)
                        {
                            if (_channel.Reader.TryRead(out var tick))
                            {
                                buffer.Add(tick);
                            }
                            else
                            {
                                // No more immediately available, wait briefly
                                try
                                {
                                    using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                                    var remaining = flushDeadline - DateTime.UtcNow;
                                    if (remaining > TimeSpan.Zero)
                                    {
                                        delayCts.CancelAfter(remaining);
                                        await _channel.Reader.WaitToReadAsync(delayCts.Token);
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                    break;
                                }
                            }
                        }

                        if (buffer.Count > 0)
                        {
                            await WriteBatch(buffer);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TickWriter] Writer loop error: {ex.Message}");
                    await Task.Delay(1000, ct);
                }
            }

            // Drain remaining on shutdown
            while (_channel.Reader.TryRead(out var remaining))
            {
                buffer.Add(remaining);
                if (buffer.Count >= BatchSize)
                {
                    await WriteBatch(buffer);
                    buffer.Clear();
                }
            }
            if (buffer.Count > 0)
            {
                await WriteBatch(buffer);
            }

            Console.WriteLine("[TickWriter] Writer loop stopped");
        }

        private async Task WriteBatch(List<TickData> ticks)
        {
            try
            {
                // Ensure partitions exist for all dates in batch
                var dates = ticks.Select(t => t.TickTime.Date).Distinct();
                foreach (var date in dates)
                {
                    await EnsurePartition(date);
                }

                await using var conn = new NpgsqlConnection(_dbConfig.ConnectionString);
                await conn.OpenAsync();

                await using var writer = await conn.BeginBinaryImportAsync(
                    "COPY tick_history (symbol, bid, ask, spread, high, low, digits, volume, direction, tick_time) FROM STDIN (FORMAT BINARY)");

                foreach (var tick in ticks)
                {
                    await writer.StartRowAsync();
                    await writer.WriteAsync(tick.Symbol, NpgsqlDbType.Text);
                    await writer.WriteAsync(tick.Bid, NpgsqlDbType.Double);
                    await writer.WriteAsync(tick.Ask, NpgsqlDbType.Double);
                    await writer.WriteAsync(tick.Spread, NpgsqlDbType.Double);
                    await writer.WriteAsync(tick.High, NpgsqlDbType.Double);
                    await writer.WriteAsync(tick.Low, NpgsqlDbType.Double);
                    await writer.WriteAsync(tick.Digits, NpgsqlDbType.Integer);
                    await writer.WriteAsync(tick.Volume, NpgsqlDbType.Double);
                    await writer.WriteAsync(tick.Direction, NpgsqlDbType.Text);
                    await writer.WriteAsync(DateTime.SpecifyKind(tick.TickTime, DateTimeKind.Utc), NpgsqlDbType.TimestampTz);
                }

                await writer.CompleteAsync();
                Console.WriteLine($"[TickWriter] Wrote {ticks.Count} ticks via COPY");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TickWriter] WriteBatch error: {ex.Message}");
            }
        }

        private static async Task<T> ExecuteScalarAsync<T>(NpgsqlConnection conn, string sql, params NpgsqlParameter[] parameters)
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            foreach (var p in parameters)
                cmd.Parameters.Add(p);
            var result = await cmd.ExecuteScalarAsync();
            return (T)(result ?? default(T)!);
        }
    }
}
