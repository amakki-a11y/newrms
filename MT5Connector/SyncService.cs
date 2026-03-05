namespace MT5Connector
{
    public class SyncService
    {
        private readonly MT5Service _mt5;
        private readonly DbService _db;

        public SyncService(MT5Service mt5, DbService db)
        {
            _mt5 = mt5;
            _db = db;
        }

        public async Task FullSync()
        {
            try
            {
                Console.WriteLine("[Sync] Starting full sync...");

                // 1. Sync symbols
                var symbolsDict = _mt5.GetSymbols();
                if (symbolsDict.Count > 0)
                {
                    var symbolsList = symbolsDict.Values.ToList();
                    await _db.UpsertSymbols(symbolsList);
                    Console.WriteLine($"[Sync] Synced {symbolsList.Count} symbols");
                }
                else
                {
                    Console.WriteLine("[Sync] No symbols available from MT5");
                }

                Console.WriteLine("[Sync] Full sync completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Sync] Full sync error: {ex.Message}");
            }
        }

        public async Task SyncAccount(long login)
        {
            try
            {
                Console.WriteLine($"[Sync] Syncing account {login}...");

                // Re-sync symbols in case new ones appeared
                var symbolsDict = _mt5.GetSymbols();
                if (symbolsDict.Count > 0)
                {
                    await _db.UpsertSymbols(symbolsDict.Values.ToList());
                }

                Console.WriteLine($"[Sync] Account {login} sync completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Sync] SyncAccount({login}) error: {ex.Message}");
            }
        }
    }
}
