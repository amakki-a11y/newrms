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

        /// <summary>
        /// Get paginated accounts list from the database.
        /// </summary>
        public async Task<PaginatedAccountResult> GetAccounts(int page = 1, int pageSize = 50, string? search = null)
        {
            try
            {
                Console.WriteLine($"[Account] GetAccounts page={page} pageSize={pageSize} search={search ?? "none"}");
                var result = await _db.GetAccounts(page, pageSize, search);
                Console.WriteLine($"[Account] Retrieved {result.Accounts.Count} accounts (total: {result.TotalCount})");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Account] Error getting accounts: {ex.Message}");
                return new PaginatedAccountResult
                {
                    Accounts = new List<AccountData>(),
                    TotalCount = 0,
                    Page = page,
                    PageSize = pageSize
                };
            }
        }

        /// <summary>
        /// Get all open positions for a specific login from the database.
        /// </summary>
        public async Task<List<PositionData>> GetPositions(long login)
        {
            try
            {
                Console.WriteLine($"[Account] GetPositions login={login}");
                var positions = await _db.GetPositions(login);
                Console.WriteLine($"[Account] Retrieved {positions.Count} positions for login {login}");
                return positions;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Account] Error getting positions for login {login}: {ex.Message}");
                return new List<PositionData>();
            }
        }

        /// <summary>
        /// Close a position via MT5 Manager API.
        /// </summary>
        public async Task<ClosePositionResult> ClosePosition(long login, long ticket, string symbol, int action, long volume)
        {
            try
            {
                Console.WriteLine($"[Account] ClosePosition login={login} ticket={ticket} symbol={symbol} action={action} volume={volume}");
                var result = await _mt5.ClosePosition(login, ticket, symbol, action, volume);
                Console.WriteLine($"[Account] ClosePosition result: success={result.Success} message={result.Message}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Account] Error closing position ticket={ticket}: {ex.Message}");
                return new ClosePositionResult
                {
                    Success = false,
                    Message = $"Error closing position: {ex.Message}",
                    Ticket = ticket
                };
            }
        }

        /// <summary>
        /// Open a new position via MT5 Manager API.
        /// </summary>
        public async Task<OpenPositionResult> OpenPosition(long login, string symbol, int action, long volume)
        {
            try
            {
                Console.WriteLine($"[Account] OpenPosition login={login} symbol={symbol} action={action} volume={volume}");
                var result = await _mt5.OpenPosition(login, symbol, action, volume);
                Console.WriteLine($"[Account] OpenPosition result: success={result.Success} ticket={result.Ticket} message={result.Message}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Account] Error opening position: {ex.Message}");
                return new OpenPositionResult
                {
                    Success = false,
                    Message = $"Error opening position: {ex.Message}",
                    Ticket = 0
                };
            }
        }

        /// <summary>
        /// Modify stop-loss and take-profit on an existing position via MT5 Manager API.
        /// </summary>
        public async Task<ModifyPositionResult> ModifyPosition(long login, long ticket, string symbol, double sl, double tp)
        {
            try
            {
                Console.WriteLine($"[Account] ModifyPosition login={login} ticket={ticket} symbol={symbol} sl={sl} tp={tp}");
                var result = await _mt5.ModifyPosition(login, ticket, symbol, sl, tp);
                Console.WriteLine($"[Account] ModifyPosition result: success={result.Success} message={result.Message}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Account] Error modifying position ticket={ticket}: {ex.Message}");
                return new ModifyPositionResult
                {
                    Success = false,
                    Message = $"Error modifying position: {ex.Message}",
                    Ticket = ticket
                };
            }
        }
    }
}
