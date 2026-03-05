namespace MT5Connector
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("[Main] MT5 Risk Management System starting...");

            // Configs
            var mt5Config = new MT5Config();
            var dbConfig = new DbConfig();

            // Services
            var mt5 = new MT5Service(mt5Config);
            var db = new DbService(dbConfig);
            var sync = new SyncService(mt5, db);
            var tickWriter = new TickWriterService(dbConfig);
            var tickHistory = new TickHistoryQuery(dbConfig);
            var accountService = new AccountService(mt5, db);
            var pnlService = new PnLService(mt5, db);
            var aiService = new AiService(accountService, db, mt5);

            // WebSocket server
            var wsServer = new TickWebSocketServer(mt5, accountService, db, tickHistory, sync, aiService);

            Console.WriteLine("[Main] All services created (stub mode)");
            Console.WriteLine("[Main] Press Enter to exit...");
            Console.ReadLine();
        }
    }
}
