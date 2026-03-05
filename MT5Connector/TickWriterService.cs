using System.Threading.Channels;

namespace MT5Connector
{
    public class TickWriterService
    {
        private readonly DbConfig _dbConfig;
        private readonly Channel<TickData> _channel;
        private Task? _writerTask;
        private CancellationTokenSource? _cts;

        public TickWriterService(DbConfig dbConfig)
        {
            _dbConfig = dbConfig;
            _channel = Channel.CreateBounded<TickData>(50000);
        }

        public void Start()
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
    }
}
