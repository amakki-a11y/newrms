namespace MT5Connector
{
    public class AccountService
    {
        private readonly MT5Service _mt5;
        private readonly DbService _db;

        public AccountService(MT5Service mt5, DbService db)
        {
            _mt5 = mt5;
            _db = db;
        }

        public async Task<PaginatedAccountResult> GetAccounts(int page = 1, int pageSize = 50, string? search = null)
        {
            throw new NotImplementedException();
        }

        public async Task<List<PositionData>> GetPositions(long login)
        {
            throw new NotImplementedException();
        }

        public async Task<ClosePositionResult> ClosePosition(long login, long ticket, string symbol, int action, long volume)
        {
            throw new NotImplementedException();
        }

        public async Task<OpenPositionResult> OpenPosition(long login, string symbol, int action, long volume)
        {
            throw new NotImplementedException();
        }

        public async Task<ModifyPositionResult> ModifyPosition(long login, long ticket, string symbol, double sl, double tp)
        {
            throw new NotImplementedException();
        }
    }
}
