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

        // ── Schema Initialization ────────────────────────────────────────

        public async Task InitializeSchema()
        {
            try
            {
                await using var conn = new NpgsqlConnection(_config.ConnectionString);
                await conn.OpenAsync();

                // Create tables
                await conn.ExecuteAsync(@"
                    CREATE TABLE IF NOT EXISTS symbols (
                        symbol TEXT PRIMARY KEY,
                        digits INT,
                        contract_size FLOAT8,
                        profit_currency TEXT,
                        margin_currency TEXT,
                        calc_mode INT,
                        category TEXT,
                        updated_at TIMESTAMPTZ DEFAULT NOW()
                    );

                    CREATE TABLE IF NOT EXISTS accounts (
                        login BIGINT PRIMARY KEY,
                        name TEXT,
                        ""group"" TEXT,
                        balance FLOAT8,
                        credit FLOAT8,
                        leverage INT,
                        equity FLOAT8,
                        margin FLOAT8,
                        margin_free FLOAT8,
                        margin_level FLOAT8,
                        profit FLOAT8,
                        floating FLOAT8,
                        open_positions INT,
                        updated_at TIMESTAMPTZ DEFAULT NOW()
                    );

                    CREATE TABLE IF NOT EXISTS positions (
                        ticket BIGINT PRIMARY KEY,
                        login BIGINT REFERENCES accounts(login) ON DELETE CASCADE,
                        symbol TEXT,
                        action INT,
                        volume BIGINT,
                        price_open FLOAT8,
                        price_current FLOAT8,
                        price_sl FLOAT8,
                        price_tp FLOAT8,
                        profit FLOAT8,
                        storage FLOAT8,
                        digits INT,
                        time_create TIMESTAMPTZ,
                        updated_at TIMESTAMPTZ DEFAULT NOW()
                    );

                    CREATE TABLE IF NOT EXISTS deals (
                        deal_id BIGINT PRIMARY KEY,
                        login BIGINT,
                        symbol TEXT,
                        action INT,
                        volume BIGINT,
                        price FLOAT8,
                        profit FLOAT8,
                        comment TEXT,
                        created_at TIMESTAMPTZ DEFAULT NOW()
                    );

                    CREATE TABLE IF NOT EXISTS tick_history (
                        symbol TEXT,
                        bid FLOAT8,
                        ask FLOAT8,
                        spread FLOAT8,
                        high FLOAT8,
                        low FLOAT8,
                        digits INT,
                        volume FLOAT8,
                        direction TEXT,
                        tick_time TIMESTAMPTZ
                    ) PARTITION BY RANGE (tick_time);
                ");

                // Create indices (IF NOT EXISTS for safety)
                await conn.ExecuteAsync(@"
                    CREATE INDEX IF NOT EXISTS idx_accounts_group ON accounts(""group"");
                    CREATE INDEX IF NOT EXISTS idx_accounts_name_lower ON accounts(lower(name));
                    CREATE INDEX IF NOT EXISTS idx_positions_login ON positions(login);
                    CREATE INDEX IF NOT EXISTS idx_positions_symbol ON positions(symbol);
                    CREATE INDEX IF NOT EXISTS idx_deals_login ON deals(login);
                    CREATE INDEX IF NOT EXISTS idx_deals_created_at ON deals(created_at DESC);
                ");

                // Create tick_history partitions for today and next few days
                var today = DateTime.UtcNow.Date;
                for (int i = 0; i < 3; i++)
                {
                    var date = today.AddDays(i);
                    await CreateTickPartitionIfNotExists(conn, date);
                }

                // Create index on tick_history partitions (applied to parent)
                // We create the index on each partition in CreateTickPartitionIfNotExists

                Console.WriteLine("[DB] Schema initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] Schema initialization error: {ex.Message}");
                throw;
            }
        }

        private async Task CreateTickPartitionIfNotExists(NpgsqlConnection conn, DateTime date)
        {
            var partitionName = $"tick_history_{date:yyyyMMdd}";
            var startDate = date.ToString("yyyy-MM-dd");
            var endDate = date.AddDays(1).ToString("yyyy-MM-dd");

            // Check if partition exists
            var exists = await conn.ExecuteScalarAsync<bool>(@"
                SELECT EXISTS (
                    SELECT 1 FROM pg_class WHERE relname = @partitionName
                )", new { partitionName });

            if (!exists)
            {
                await conn.ExecuteAsync($@"
                    CREATE TABLE {partitionName} PARTITION OF tick_history
                    FOR VALUES FROM ('{startDate}') TO ('{endDate}')");

                await conn.ExecuteAsync($@"
                    CREATE INDEX IF NOT EXISTS idx_{partitionName}_symbol_time
                    ON {partitionName}(symbol, tick_time DESC)");

                Console.WriteLine($"[DB] Created partition {partitionName}");
            }
        }

        // ── Symbols ─────────────────────────────────────────────────────

        public async Task UpsertSymbols(List<SymbolInfo> symbols)
        {
            if (symbols.Count == 0) return;

            try
            {
                await using var conn = new NpgsqlConnection(_config.ConnectionString);
                await conn.OpenAsync();

                const string sql = @"
                    INSERT INTO symbols (symbol, digits, contract_size, profit_currency, margin_currency, calc_mode, category, updated_at)
                    VALUES (@Symbol, @Digits, @ContractSize, @ProfitCurrency, @MarginCurrency, @CalcMode, @Category, NOW())
                    ON CONFLICT (symbol) DO UPDATE SET
                        digits = EXCLUDED.digits,
                        contract_size = EXCLUDED.contract_size,
                        profit_currency = EXCLUDED.profit_currency,
                        margin_currency = EXCLUDED.margin_currency,
                        calc_mode = EXCLUDED.calc_mode,
                        category = EXCLUDED.category,
                        updated_at = NOW()";

                await conn.ExecuteAsync(sql, symbols);
                Console.WriteLine($"[DB] Upserted {symbols.Count} symbols");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] UpsertSymbols error: {ex.Message}");
            }
        }

        // ── Accounts ────────────────────────────────────────────────────

        public async Task UpsertAccounts(List<AccountData> accounts)
        {
            if (accounts.Count == 0) return;

            try
            {
                await using var conn = new NpgsqlConnection(_config.ConnectionString);
                await conn.OpenAsync();

                const string sql = @"
                    INSERT INTO accounts (login, name, ""group"", balance, credit, leverage, equity, margin, margin_free, margin_level, profit, floating, open_positions, updated_at)
                    VALUES (@Login, @Name, @Group, @Balance, @Credit, @Leverage, @Equity, @Margin, @MarginFree, @MarginLevel, @Profit, @Floating, @OpenPositions, NOW())
                    ON CONFLICT (login) DO UPDATE SET
                        name = EXCLUDED.name,
                        ""group"" = EXCLUDED.""group"",
                        balance = EXCLUDED.balance,
                        credit = EXCLUDED.credit,
                        leverage = EXCLUDED.leverage,
                        equity = EXCLUDED.equity,
                        margin = EXCLUDED.margin,
                        margin_free = EXCLUDED.margin_free,
                        margin_level = EXCLUDED.margin_level,
                        profit = EXCLUDED.profit,
                        floating = EXCLUDED.floating,
                        open_positions = EXCLUDED.open_positions,
                        updated_at = NOW()";

                // Batch in chunks of 500
                var chunks = ChunkList(accounts, 500);
                foreach (var chunk in chunks)
                {
                    await conn.ExecuteAsync(sql, chunk);
                }

                Console.WriteLine($"[DB] Upserted {accounts.Count} accounts");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] UpsertAccounts error: {ex.Message}");
            }
        }

        public async Task<PaginatedAccountResult> GetAccounts(int page = 1, int pageSize = 50, string? search = null)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_config.ConnectionString);
                await conn.OpenAsync();

                var whereClause = "";
                object parameters;

                if (!string.IsNullOrWhiteSpace(search))
                {
                    whereClause = "WHERE lower(name) LIKE @search OR CAST(login AS TEXT) LIKE @search";
                    var searchPattern = $"%{search.ToLower()}%";
                    parameters = new { search = searchPattern, limit = pageSize, offset = (page - 1) * pageSize };
                }
                else
                {
                    parameters = new { limit = pageSize, offset = (page - 1) * pageSize };
                }

                var countSql = $"SELECT COUNT(*) FROM accounts {whereClause}";
                var totalCount = await conn.ExecuteScalarAsync<int>(countSql, parameters);

                var dataSql = $@"
                    SELECT login as Login, name as Name, ""group"" as ""Group"", balance as Balance, credit as Credit,
                           leverage as Leverage, equity as Equity, margin as Margin, margin_free as MarginFree,
                           margin_level as MarginLevel, profit as Profit, floating as Floating,
                           open_positions as OpenPositions, updated_at as UpdatedAt
                    FROM accounts {whereClause}
                    ORDER BY login
                    LIMIT @limit OFFSET @offset";

                var accountsList = (await conn.QueryAsync<AccountData>(dataSql, parameters)).ToList();

                // Get summary
                var summary = await GetAccountSummaryInternal(conn);

                return new PaginatedAccountResult
                {
                    Accounts = accountsList,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    Summary = summary
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] GetAccounts error: {ex.Message}");
                return new PaginatedAccountResult { Page = page, PageSize = pageSize };
            }
        }

        public async Task<AccountData?> GetAccount(long login)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_config.ConnectionString);
                await conn.OpenAsync();

                const string sql = @"
                    SELECT login as Login, name as Name, ""group"" as ""Group"", balance as Balance, credit as Credit,
                           leverage as Leverage, equity as Equity, margin as Margin, margin_free as MarginFree,
                           margin_level as MarginLevel, profit as Profit, floating as Floating,
                           open_positions as OpenPositions, updated_at as UpdatedAt
                    FROM accounts WHERE login = @login";

                return await conn.QueryFirstOrDefaultAsync<AccountData>(sql, new { login });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] GetAccount error: {ex.Message}");
                return null;
            }
        }

        // ── Positions ───────────────────────────────────────────────────

        public async Task UpsertPositions(long login, List<PositionData> positions)
        {
            if (positions.Count == 0) return;

            try
            {
                await using var conn = new NpgsqlConnection(_config.ConnectionString);
                await conn.OpenAsync();

                const string sql = @"
                    INSERT INTO positions (ticket, login, symbol, action, volume, price_open, price_current, price_sl, price_tp, profit, storage, digits, time_create, updated_at)
                    VALUES (@Ticket, @Login, @Symbol, @Action, @Volume, @PriceOpen, @PriceCurrent, @PriceSl, @PriceTp, @Profit, @Storage, @Digits, @TimeCreate, NOW())
                    ON CONFLICT (ticket) DO UPDATE SET
                        login = EXCLUDED.login,
                        symbol = EXCLUDED.symbol,
                        action = EXCLUDED.action,
                        volume = EXCLUDED.volume,
                        price_open = EXCLUDED.price_open,
                        price_current = EXCLUDED.price_current,
                        price_sl = EXCLUDED.price_sl,
                        price_tp = EXCLUDED.price_tp,
                        profit = EXCLUDED.profit,
                        storage = EXCLUDED.storage,
                        digits = EXCLUDED.digits,
                        time_create = EXCLUDED.time_create,
                        updated_at = NOW()";

                // Set login on each position
                foreach (var p in positions)
                {
                    p.Login = login;
                }

                var chunks = ChunkList(positions, 500);
                foreach (var chunk in chunks)
                {
                    await conn.ExecuteAsync(sql, chunk);
                }

                Console.WriteLine($"[DB] Upserted {positions.Count} positions for login {login}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] UpsertPositions error: {ex.Message}");
            }
        }

        public async Task<List<PositionData>> GetPositions(long login)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_config.ConnectionString);
                await conn.OpenAsync();

                const string sql = @"
                    SELECT ticket as Ticket, login as Login, symbol as Symbol, action as Action, volume as Volume,
                           price_open as PriceOpen, price_current as PriceCurrent, price_sl as PriceSl,
                           price_tp as PriceTp, profit as Profit, storage as Storage, digits as Digits,
                           time_create as TimeCreate, updated_at as UpdatedAt
                    FROM positions WHERE login = @login
                    ORDER BY time_create DESC";

                return (await conn.QueryAsync<PositionData>(sql, new { login })).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] GetPositions error: {ex.Message}");
                return new List<PositionData>();
            }
        }

        public async Task DeletePositionsNotIn(long login, List<long> activeTickets)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_config.ConnectionString);
                await conn.OpenAsync();

                if (activeTickets.Count == 0)
                {
                    // Delete all positions for this login
                    await conn.ExecuteAsync("DELETE FROM positions WHERE login = @login", new { login });
                }
                else
                {
                    await conn.ExecuteAsync(
                        "DELETE FROM positions WHERE login = @login AND ticket != ALL(@tickets)",
                        new { login, tickets = activeTickets.ToArray() });
                }

                Console.WriteLine($"[DB] Cleaned stale positions for login {login}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] DeletePositionsNotIn error: {ex.Message}");
            }
        }

        // ── Deals ───────────────────────────────────────────────────────

        public async Task InsertDeal(DealEventData deal)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_config.ConnectionString);
                await conn.OpenAsync();

                const string sql = @"
                    INSERT INTO deals (deal_id, login, symbol, action, volume, price, profit, comment, created_at)
                    VALUES (@DealId, @Login, @Symbol, @Action, @Volume, @Price, @Profit, @Comment, @CreatedAt)
                    ON CONFLICT (deal_id) DO NOTHING";

                await conn.ExecuteAsync(sql, deal);
                Console.WriteLine($"[DB] Inserted deal {deal.DealId} for login {deal.Login}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] InsertDeal error: {ex.Message}");
            }
        }

        public async Task<List<DealEventData>> GetDealHistory(long? login, DateTime from, DateTime to)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_config.ConnectionString);
                await conn.OpenAsync();

                var loginFilter = login.HasValue ? "AND login = @login" : "";
                var sql = $@"
                    SELECT deal_id as DealId, login as Login, symbol as Symbol, action as Action,
                           volume as Volume, price as Price, profit as Profit, comment as Comment,
                           created_at as CreatedAt
                    FROM deals
                    WHERE created_at >= @from AND created_at <= @to {loginFilter}
                    ORDER BY created_at DESC";

                return (await conn.QueryAsync<DealEventData>(sql, new { login, from, to })).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] GetDealHistory error: {ex.Message}");
                return new List<DealEventData>();
            }
        }

        // ── Dashboard Queries ───────────────────────────────────────────

        public async Task<AccountSummary> GetAccountSummary()
        {
            try
            {
                await using var conn = new NpgsqlConnection(_config.ConnectionString);
                await conn.OpenAsync();
                return await GetAccountSummaryInternal(conn);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] GetAccountSummary error: {ex.Message}");
                return new AccountSummary();
            }
        }

        private async Task<AccountSummary> GetAccountSummaryInternal(NpgsqlConnection conn)
        {
            const string sql = @"
                SELECT
                    COALESCE(COUNT(*), 0) as TotalAccounts,
                    COALESCE(SUM(balance), 0) as TotalBalance,
                    COALESCE(SUM(equity), 0) as TotalEquity,
                    COALESCE(SUM(margin), 0) as TotalMargin,
                    COALESCE(SUM(margin_free), 0) as TotalMarginFree,
                    COALESCE(SUM(profit), 0) as TotalProfit
                FROM accounts";

            var result = await conn.QueryFirstOrDefaultAsync<AccountSummary>(sql);
            return result ?? new AccountSummary();
        }

        public async Task<List<SymbolExposure>> GetSymbolExposure()
        {
            try
            {
                await using var conn = new NpgsqlConnection(_config.ConnectionString);
                await conn.OpenAsync();

                const string sql = @"
                    SELECT
                        symbol as Symbol,
                        COALESCE(SUM(CASE WHEN action = 0 THEN volume ELSE 0 END), 0) as LongVolume,
                        COALESCE(SUM(CASE WHEN action = 1 THEN volume ELSE 0 END), 0) as ShortVolume,
                        COALESCE(SUM(CASE WHEN action = 0 THEN volume ELSE -volume END), 0) as NetVolume,
                        COALESCE(SUM(CASE WHEN action = 0 THEN profit ELSE 0 END), 0) as LongProfit,
                        COALESCE(SUM(CASE WHEN action = 1 THEN profit ELSE 0 END), 0) as ShortProfit,
                        COALESCE(SUM(profit), 0) as NetProfit
                    FROM positions
                    GROUP BY symbol
                    ORDER BY ABS(COALESCE(SUM(profit), 0)) DESC";

                return (await conn.QueryAsync<SymbolExposure>(sql)).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] GetSymbolExposure error: {ex.Message}");
                return new List<SymbolExposure>();
            }
        }

        public async Task<List<AccountMover>> GetTopMovers(int count = 5)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_config.ConnectionString);
                await conn.OpenAsync();

                const string sql = @"
                    SELECT login as Login, name as Name, profit as Profit, profit as ProfitChange
                    FROM accounts
                    ORDER BY ABS(profit) DESC
                    LIMIT @count";

                return (await conn.QueryAsync<AccountMover>(sql, new { count })).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] GetTopMovers error: {ex.Message}");
                return new List<AccountMover>();
            }
        }

        public async Task<List<DealHistoryBucket>> GetDealHistoryBuckets(DateTime from, DateTime to, string interval = "1 hour")
        {
            try
            {
                await using var conn = new NpgsqlConnection(_config.ConnectionString);
                await conn.OpenAsync();

                var sql = @"
                    SELECT
                        date_bin(@interval::interval, created_at, '2000-01-01'::timestamptz) as BucketTime,
                        COALESCE(SUM(profit), 0) as TotalProfit,
                        COUNT(*)::int as DealCount
                    FROM deals
                    WHERE created_at >= @from AND created_at <= @to
                    GROUP BY BucketTime
                    ORDER BY BucketTime";

                return (await conn.QueryAsync<DealHistoryBucket>(sql, new { interval, from, to })).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] GetDealHistoryBuckets error: {ex.Message}");
                return new List<DealHistoryBucket>();
            }
        }

        // ── Batch Update Profits ────────────────────────────────────────

        public async Task BatchUpdateProfits(List<PositionData> positions)
        {
            if (positions.Count == 0) return;

            try
            {
                await using var conn = new NpgsqlConnection(_config.ConnectionString);
                await conn.OpenAsync();

                const string sql = @"
                    UPDATE positions SET profit = @Profit, price_current = @PriceCurrent, updated_at = NOW()
                    WHERE ticket = @Ticket";

                var updates = positions.Select(p => new { p.Profit, p.PriceCurrent, p.Ticket });

                var chunks = ChunkList(updates.ToList(), 500);
                foreach (var chunk in chunks)
                {
                    await conn.ExecuteAsync(sql, chunk);
                }

                Console.WriteLine($"[DB] Batch updated profits for {positions.Count} positions");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] BatchUpdateProfits error: {ex.Message}");
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private static List<List<T>> ChunkList<T>(List<T> source, int chunkSize)
        {
            var chunks = new List<List<T>>();
            for (int i = 0; i < source.Count; i += chunkSize)
            {
                chunks.Add(source.GetRange(i, Math.Min(chunkSize, source.Count - i)));
            }
            return chunks;
        }
    }
}
