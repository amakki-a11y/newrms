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
            throw new NotImplementedException();
        }

        public async Task SyncAccount(long login)
        {
            throw new NotImplementedException();
        }
    }
}
