using System.Threading.Channels;

namespace MT5Connector
{
    public class MT5Service
    {
        public event Action<TickData>? OnTickReceived;
        public event Action<DealEventData>? OnDealEvent;

        private readonly MT5Config _config;
        private readonly Dictionary<string, TickData> _latestTicks = new();
        private readonly Dictionary<string, SymbolInfo> _symbols = new();
        private bool _connected = false;

        public bool IsConnected => _connected;

        public MT5Service(MT5Config config)
        {
            _config = config;
        }

        public void Connect()
        {
            throw new NotImplementedException();
        }

        public void Disconnect()
        {
            throw new NotImplementedException();
        }

        public TickData? GetTick(string symbol)
        {
            _latestTicks.TryGetValue(symbol, out var tick);
            return tick;
        }

        public Dictionary<string, TickData> GetAllTicks()
        {
            return new Dictionary<string, TickData>(_latestTicks);
        }

        public Dictionary<string, SymbolInfo> GetSymbols()
        {
            return new Dictionary<string, SymbolInfo>(_symbols);
        }

        public SymbolInfo? GetSymbolInfo(string symbol)
        {
            _symbols.TryGetValue(symbol, out var info);
            return info;
        }

        // Account operations via MT5 Manager API
        public Task<ClosePositionResult> ClosePosition(long login, long ticket, string symbol, int action, long volume)
        {
            throw new NotImplementedException();
        }

        public Task<OpenPositionResult> OpenPosition(long login, string symbol, int action, long volume)
        {
            throw new NotImplementedException();
        }

        public Task<ModifyPositionResult> ModifyPosition(long login, long ticket, string symbol, double sl, double tp)
        {
            throw new NotImplementedException();
        }

        // Raise tick event (called by tick sink or mock generator)
        protected void RaiseTickReceived(TickData tick)
        {
            _latestTicks[tick.Symbol] = tick;
            OnTickReceived?.Invoke(tick);
        }

        // Raise deal event (called by deal sink)
        protected void RaiseDealEvent(DealEventData deal)
        {
            OnDealEvent?.Invoke(deal);
        }
    }
}
