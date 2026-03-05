using Fleck;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MT5Connector
{
    public class TickWebSocketServer
    {
        private readonly MT5Service _mt5;
        private readonly AccountService _accountService;
        private readonly DbService _db;
        private readonly TickHistoryQuery _tickHistory;
        private readonly AiService? _ai;
        private readonly SyncService _sync;
        private WebSocketServer? _server;
        private readonly List<IWebSocketConnection> _clients = new();

        public TickWebSocketServer(
            MT5Service mt5,
            AccountService accountService,
            DbService db,
            TickHistoryQuery tickHistory,
            SyncService sync,
            AiService? ai = null)
        {
            _mt5 = mt5;
            _accountService = accountService;
            _db = db;
            _tickHistory = tickHistory;
            _sync = sync;
            _ai = ai;
        }

        public void Start(int port)
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            _server?.Dispose();
        }

        public void BroadcastTick(TickData tick)
        {
            throw new NotImplementedException();
        }

        public void BroadcastDealEvent(DealEventData deal)
        {
            throw new NotImplementedException();
        }
    }
}
