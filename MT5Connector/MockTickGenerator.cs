namespace MT5Connector
{
    public class MockTickGenerator
    {
        public event Action<TickData>? OnTickGenerated;

        private Timer? _timer;
        private readonly int _intervalMs;

        public MockTickGenerator(int intervalMs = 200)
        {
            _intervalMs = intervalMs;
        }

        public void Start()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        public List<SymbolInfo> GetMockSymbols()
        {
            throw new NotImplementedException();
        }
    }
}
