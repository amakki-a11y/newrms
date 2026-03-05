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

            // Services (created in dependency order)
            var mt5 = new MT5Service(mt5Config);
            var db = new DbService(dbConfig);
            TickWriterService? tickWriter = null;
            TickHistoryQuery? tickHistory = null;
            SyncService? sync = null;
            AccountService? accountService = null;
            PnLService? pnlService = null;
            AiService? aiService = null;
            TickWebSocketServer? wsServer = null;

            try
            {
                // Initialize database schema
                try
                {
                    await db.InitializeSchema();
                    Console.WriteLine("[Main] Database schema initialized");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Main] Database initialization failed: {ex.Message}");
                    Console.WriteLine("[Main] Continuing without DB — mock mode is still viable");
                }

                // Create remaining services
                sync = new SyncService(mt5, db);
                tickWriter = new TickWriterService(dbConfig);
                tickWriter.Start();
                Console.WriteLine("[Main] TickWriter started");

                tickHistory = new TickHistoryQuery(dbConfig);
                accountService = new AccountService(mt5, db);

                pnlService = new PnLService(mt5, db);
                pnlService.Start();
                Console.WriteLine("[Main] PnL service started");

                aiService = new AiService(accountService, db, mt5);

                wsServer = new TickWebSocketServer(mt5, accountService, db, tickHistory, sync, aiService);

                // Connect to MT5 (will fall back to mock mode if DLLs are unavailable)
                mt5.Connect();

                // Run initial sync
                try
                {
                    await sync.FullSync();
                    Console.WriteLine("[Main] Initial sync completed");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Main] Initial sync failed: {ex.Message}");
                }

                // Wire tick events
                mt5.OnTickReceived += tick =>
                {
                    wsServer.BroadcastTick(tick);
                    _ = tickWriter.EnqueueTick(tick);
                };

                // Wire deal events
                mt5.OnDealEvent += deal =>
                {
                    wsServer.BroadcastDealEvent(deal);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await sync.SyncAccount(deal.Login);
                            await db.InsertDeal(deal);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Main] Error processing deal event for login {deal.Login}: {ex.Message}");
                        }
                    });
                };

                // Start WebSocket server
                wsServer.Start(mt5Config.WsPort);
                Console.WriteLine($"[Main] WebSocket server running on port {mt5Config.WsPort}");

                Console.WriteLine("[Main] All services started successfully");
                Console.WriteLine("[Main] Press Ctrl+C or Enter to shut down...");

                // Handle graceful shutdown on Ctrl+C
                var shutdownEvent = new ManualResetEventSlim(false);
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    Console.WriteLine("\n[Main] Shutdown signal received...");
                    shutdownEvent.Set();
                };

                // Wait for either Enter key or Ctrl+C
                var enterTask = Task.Run(() => Console.ReadLine());
                var ctrlCTask = Task.Run(() => shutdownEvent.Wait());
                await Task.WhenAny(enterTask, ctrlCTask);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Main] Fatal error: {ex.Message}");
                Console.WriteLine($"[Main] Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // Graceful shutdown
                Console.WriteLine("[Main] Shutting down services...");

                try { wsServer?.Stop(); } catch { }
                try { pnlService?.Stop(); } catch { }
                try { tickWriter?.Stop(); } catch { }
                try { mt5.Disconnect(); } catch { }

                Console.WriteLine("[Main] Shutdown complete");
            }
        }
    }
}
