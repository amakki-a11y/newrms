namespace MT5Connector
{
    public class PnLService
    {
        private readonly MT5Service _mt5;
        private readonly DbService _db;
        private Timer? _timer;
        private bool _isProcessing = false;

        public PnLService(MT5Service mt5, DbService db)
        {
            _mt5 = mt5;
            _db = db;
        }

        /// <summary>
        /// Start the 1-second P&L recalculation timer.
        /// Each cycle: load all accounts, get their positions, recalculate profits using
        /// current tick data, persist updated profits, and update account-level equity/floating.
        /// </summary>
        public void Start()
        {
            Console.WriteLine("[PnL] Starting P&L recalculation service (1-second interval)");
            _timer = new Timer(async _ => await RecalculationCycle(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        public void Stop()
        {
            Console.WriteLine("[PnL] Stopping P&L recalculation service");
            _timer?.Dispose();
            _timer = null;
        }

        /// <summary>
        /// Single recalculation cycle. Skips if previous cycle is still running.
        /// </summary>
        private async Task RecalculationCycle()
        {
            // Prevent overlapping executions
            if (_isProcessing)
                return;

            _isProcessing = true;

            try
            {
                // 1. Get current ticks and symbol info from MT5
                var ticks = _mt5.GetAllTicks();
                var symbols = _mt5.GetSymbols();

                if (ticks.Count == 0)
                {
                    // No tick data available yet, skip this cycle
                    return;
                }

                // 2. Get all accounts from DB (fetch a large page to cover all)
                var accountResult = await _db.GetAccounts(1, 10000, null);
                var accounts = accountResult.Accounts;

                if (accounts.Count == 0)
                    return;

                // 3. Collect all positions across all accounts
                var allPositions = new List<PositionData>();
                var positionsByLogin = new Dictionary<long, List<PositionData>>();

                foreach (var account in accounts)
                {
                    try
                    {
                        var positions = await _db.GetPositions(account.Login);
                        if (positions.Count > 0)
                        {
                            allPositions.AddRange(positions);
                            positionsByLogin[account.Login] = positions;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[PnL] Error fetching positions for login {account.Login}: {ex.Message}");
                    }
                }

                if (allPositions.Count == 0)
                    return;

                // 4. Recalculate all profits using PnLEngine
                PnLEngine.RecalculateAllProfits(allPositions, ticks, symbols);

                // 5. Persist updated position profits to DB
                await _db.BatchUpdateProfits(allPositions);

                // 6. Update account-level equity, profit, floating based on position totals
                var accountUpdates = new List<AccountData>();

                foreach (var account in accounts)
                {
                    if (positionsByLogin.TryGetValue(account.Login, out var positions))
                    {
                        double totalFloating = positions.Sum(p => p.Profit);
                        account.Floating = Math.Round(totalFloating, 2);
                        account.Profit = Math.Round(totalFloating, 2);
                        account.Equity = Math.Round(account.Balance + account.Credit + totalFloating, 2);
                        account.OpenPositions = positions.Count;

                        // Recalculate margin level: (Equity / Margin) * 100
                        if (account.Margin > 0)
                        {
                            account.MarginLevel = Math.Round((account.Equity / account.Margin) * 100.0, 2);
                        }
                        else
                        {
                            account.MarginLevel = 0;
                        }

                        account.MarginFree = Math.Round(account.Equity - account.Margin, 2);
                        account.UpdatedAt = DateTime.UtcNow;
                        accountUpdates.Add(account);
                    }
                }

                if (accountUpdates.Count > 0)
                {
                    await _db.UpsertAccounts(accountUpdates);
                }
            }
            catch (Exception ex)
            {
                // Graceful error handling - if DB is unavailable or any error occurs, skip this cycle
                Console.WriteLine($"[PnL] Recalculation cycle error: {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
            }
        }
    }
}
