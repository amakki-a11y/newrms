using Npgsql;
using Dapper;

namespace MT5Connector
{
    public class DbService
    {
        private readonly DbConfig _config;

        public DbService(DbConfig config)
        {
            _config = config;
        }

        public async Task InitializeSchema()
        {
            throw new NotImplementedException();
        }

        // Symbols
        public async Task UpsertSymbols(List<SymbolInfo> symbols)
        {
            throw new NotImplementedException();
        }

        // Accounts
        public async Task UpsertAccounts(List<AccountData> accounts)
        {
            throw new NotImplementedException();
        }

        public async Task<PaginatedAccountResult> GetAccounts(int page = 1, int pageSize = 50, string? search = null)
        {
            throw new NotImplementedException();
        }

        public async Task<AccountData?> GetAccount(long login)
        {
            throw new NotImplementedException();
        }

        // Positions
        public async Task UpsertPositions(long login, List<PositionData> positions)
        {
            throw new NotImplementedException();
        }

        public async Task<List<PositionData>> GetPositions(long login)
        {
            throw new NotImplementedException();
        }

        public async Task DeletePositionsNotIn(long login, List<long> activeTickets)
        {
            throw new NotImplementedException();
        }

        // Deals
        public async Task InsertDeal(DealEventData deal)
        {
            throw new NotImplementedException();
        }

        public async Task<List<DealEventData>> GetDealHistory(long? login, DateTime from, DateTime to)
        {
            throw new NotImplementedException();
        }

        // Dashboard queries
        public async Task<AccountSummary> GetAccountSummary()
        {
            throw new NotImplementedException();
        }

        public async Task<List<SymbolExposure>> GetSymbolExposure()
        {
            throw new NotImplementedException();
        }

        public async Task<List<AccountMover>> GetTopMovers(int count = 5)
        {
            throw new NotImplementedException();
        }

        public async Task<List<DealHistoryBucket>> GetDealHistoryBuckets(DateTime from, DateTime to, string interval = "1 hour")
        {
            throw new NotImplementedException();
        }

        // Batch update profits
        public async Task BatchUpdateProfits(List<PositionData> positions)
        {
            throw new NotImplementedException();
        }
    }
}
