using Npgsql;
using Dapper;

namespace MT5Connector
{
    public class OhlcCandle
    {
        public DateTime Time { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }
    }

    public class TickHistoryQuery
    {
        private readonly DbConfig _dbConfig;

        private static readonly Dictionary<string, string> IntervalMap = new()
        {
            { "1m", "1 minute" },
            { "5m", "5 minutes" },
            { "15m", "15 minutes" },
            { "1h", "1 hour" },
            { "4h", "4 hours" },
            { "1d", "1 day" }
        };

        public TickHistoryQuery(DbConfig dbConfig)
        {
            _dbConfig = dbConfig;
        }

        public async Task<List<OhlcCandle>> GetCandles(string symbol, string interval, DateTime from, DateTime to, int limit = 500)
        {
            try
            {
                if (!IntervalMap.TryGetValue(interval, out var pgInterval))
                {
                    Console.WriteLine($"[DB] Unknown interval '{interval}', defaulting to 1 hour");
                    pgInterval = "1 hour";
                }

                await using var conn = new NpgsqlConnection(_dbConfig.ConnectionString);
                await conn.OpenAsync();

                // Use a practical approach for OHLC:
                // - Open: bid of the tick with the earliest tick_time in the bucket
                // - High: max(bid) in the bucket
                // - Low: min(bid) in the bucket
                // - Close: bid of the tick with the latest tick_time in the bucket
                // - Volume: sum(volume) in the bucket
                var sql = @"
                    WITH buckets AS (
                        SELECT
                            date_bin(@pgInterval::interval, tick_time, '2000-01-01'::timestamptz) AS bucket_time,
                            bid,
                            volume,
                            tick_time
                        FROM tick_history
                        WHERE symbol = @symbol AND tick_time >= @from AND tick_time <= @to
                    ),
                    agg AS (
                        SELECT
                            bucket_time,
                            MAX(bid) AS high,
                            MIN(bid) AS low,
                            SUM(volume) AS total_volume,
                            MIN(tick_time) AS first_tick_time,
                            MAX(tick_time) AS last_tick_time
                        FROM buckets
                        GROUP BY bucket_time
                    )
                    SELECT
                        a.bucket_time AS Time,
                        (SELECT b.bid FROM buckets b WHERE b.bucket_time = a.bucket_time AND b.tick_time = a.first_tick_time LIMIT 1) AS Open,
                        a.high AS High,
                        a.low AS Low,
                        (SELECT b.bid FROM buckets b WHERE b.bucket_time = a.bucket_time AND b.tick_time = a.last_tick_time LIMIT 1) AS Close,
                        a.total_volume AS Volume
                    FROM agg a
                    ORDER BY a.bucket_time
                    LIMIT @limit";

                var candles = await conn.QueryAsync<OhlcCandle>(sql, new
                {
                    pgInterval,
                    symbol,
                    from,
                    to,
                    limit
                });

                return candles.ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] GetCandles error: {ex.Message}");
                return new List<OhlcCandle>();
            }
        }
    }
}
