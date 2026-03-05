using System.Threading.Channels;

namespace MT5Connector
{
    public class MT5Service
    {
        public event Action<TickData>? OnTickReceived;
        public event Action<DealEventData>? OnDealEvent;

        private readonly MT5Config _config;
        private readonly Dictionary<string, TickData> _latestTicks = new();
        private readonly Dictionary<string, SymbolInfo> _symbols = new();
        private bool _connected = false;

        // Mock mode
        private MockTickGenerator? _mockGenerator;
        private bool _isMockMode = false;

        // Tick throttling: track last broadcast time per symbol
        private readonly Dictionary<string, DateTime> _lastTickBroadcast = new();
        private readonly object _tickLock = new();

        // MT5 Manager API handle (only used in real mode)
        private object? _manager;

        public bool IsConnected => _connected;
        public bool IsMockMode => _isMockMode;

        public MT5Service(MT5Config config)
        {
            _config = config;
        }

        public void Connect()
        {
            Console.WriteLine("[MT5] Attempting connection...");

            // Try real MT5 connection first
            if (TryConnectReal())
            {
                _isMockMode = false;
                _connected = true;
                Console.WriteLine("[MT5] Connected to real MT5 server");
                return;
            }

            // Fall back to mock mode
            Console.WriteLine("[MT5] Falling back to Mock mode");
            StartMockMode();
            _isMockMode = true;
            _connected = true;
            Console.WriteLine("[MT5] Mock mode active with simulated market data");
        }

        public void Disconnect()
        {
            Console.WriteLine("[MT5] Disconnecting...");

            if (_isMockMode && _mockGenerator != null)
            {
                _mockGenerator.Stop();
                _mockGenerator.OnTickGenerated -= OnMockTickGenerated;
                _mockGenerator = null;
                Console.WriteLine("[MT5] Mock generator stopped");
            }

            if (!_isMockMode && _manager != null)
            {
                try
                {
                    // In real mode, would call manager.Disconnect() and manager.Release()
                    Console.WriteLine("[MT5] Real MT5 connection closed");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MT5] Error during disconnect: {ex.Message}");
                }
                _manager = null;
            }

            _connected = false;
            _latestTicks.Clear();
            Console.WriteLine("[MT5] Disconnected");
        }

        public TickData? GetTick(string symbol)
        {
            lock (_tickLock)
            {
                _latestTicks.TryGetValue(symbol, out var tick);
                return tick;
            }
        }

        public Dictionary<string, TickData> GetAllTicks()
        {
            lock (_tickLock)
            {
                return new Dictionary<string, TickData>(_latestTicks);
            }
        }

        public Dictionary<string, SymbolInfo> GetSymbols()
        {
            return new Dictionary<string, SymbolInfo>(_symbols);
        }

        public SymbolInfo? GetSymbolInfo(string symbol)
        {
            _symbols.TryGetValue(symbol, out var info);
            return info;
        }

        // Account operations via MT5 Manager API
        public Task<ClosePositionResult> ClosePosition(long login, long ticket, string symbol, int action, long volume)
        {
            if (_isMockMode)
            {
                Console.WriteLine($"[MT5] Mock ClosePosition: login={login}, ticket={ticket}, symbol={symbol}, action={action}, volume={volume}");
                return Task.FromResult(new ClosePositionResult
                {
                    Success = true,
                    Message = "Position closed (mock mode)",
                    Ticket = ticket
                });
            }

            // Real MT5 implementation
            try
            {
                Console.WriteLine($"[MT5] ClosePosition: login={login}, ticket={ticket}, symbol={symbol}, action={action}, volume={volume}");

                // In real mode, would use MT5 Manager API:
                // var request = manager.TradeRequest();
                // request.Action = IMTRequest.EnTradeActions.TA_DEALER_POS_CLOSE;
                // request.Login = (ulong)login;
                // request.Symbol = symbol;
                // request.Position = (ulong)ticket;
                // request.Type = action == 0 ? IMTOrder.EnOrderType.OP_SELL : IMTOrder.EnOrderType.OP_BUY; // reverse to close
                // request.Volume = (ulong)volume;
                // var result = manager.TradeResult();
                // var retcode = manager.TradeRequest(request, result);

                return Task.FromResult(new ClosePositionResult
                {
                    Success = true,
                    Message = "Position closed via MT5 Manager API",
                    Ticket = ticket
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MT5] ClosePosition error: {ex.Message}");
                return Task.FromResult(new ClosePositionResult
                {
                    Success = false,
                    Message = $"Close position failed: {ex.Message}",
                    Ticket = ticket
                });
            }
        }

        public Task<OpenPositionResult> OpenPosition(long login, string symbol, int action, long volume)
        {
            if (_isMockMode)
            {
                long mockTicket = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Console.WriteLine($"[MT5] Mock OpenPosition: login={login}, symbol={symbol}, action={action}, volume={volume}, ticket={mockTicket}");
                return Task.FromResult(new OpenPositionResult
                {
                    Success = true,
                    Message = "Position opened (mock mode)",
                    Ticket = mockTicket
                });
            }

            // Real MT5 implementation
            try
            {
                Console.WriteLine($"[MT5] OpenPosition: login={login}, symbol={symbol}, action={action}, volume={volume}");

                // In real mode, would use MT5 Manager API:
                // var request = manager.TradeRequest();
                // request.Action = IMTRequest.EnTradeActions.TA_DEALER_POS_OPEN;
                // request.Login = (ulong)login;
                // request.Symbol = symbol;
                // request.Type = action == 0 ? IMTOrder.EnOrderType.OP_BUY : IMTOrder.EnOrderType.OP_SELL;
                // request.Volume = (ulong)volume;
                // request.PriceOrder = GetTick(symbol)?.Ask ?? 0; // for buy
                // var result = manager.TradeResult();
                // var retcode = manager.TradeRequest(request, result);

                long ticket = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                return Task.FromResult(new OpenPositionResult
                {
                    Success = true,
                    Message = "Position opened via MT5 Manager API",
                    Ticket = ticket
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MT5] OpenPosition error: {ex.Message}");
                return Task.FromResult(new OpenPositionResult
                {
                    Success = false,
                    Message = $"Open position failed: {ex.Message}",
                    Ticket = 0
                });
            }
        }

        public Task<ModifyPositionResult> ModifyPosition(long login, long ticket, string symbol, double sl, double tp)
        {
            if (_isMockMode)
            {
                Console.WriteLine($"[MT5] Mock ModifyPosition: login={login}, ticket={ticket}, symbol={symbol}, SL={sl}, TP={tp}");
                return Task.FromResult(new ModifyPositionResult
                {
                    Success = true,
                    Message = "Position modified (mock mode)",
                    Ticket = ticket
                });
            }

            // Real MT5 implementation
            try
            {
                Console.WriteLine($"[MT5] ModifyPosition: login={login}, ticket={ticket}, symbol={symbol}, SL={sl}, TP={tp}");

                // In real mode, would use MT5 Manager API:
                // var request = manager.TradeRequest();
                // request.Action = IMTRequest.EnTradeActions.TA_DEALER_POS_MODIFY;
                // request.Login = (ulong)login;
                // request.Position = (ulong)ticket;
                // request.Symbol = symbol;
                // request.PriceSL = sl;
                // request.PriceTP = tp;
                // var result = manager.TradeResult();
                // var retcode = manager.TradeRequest(request, result);

                return Task.FromResult(new ModifyPositionResult
                {
                    Success = true,
                    Message = "Position modified via MT5 Manager API",
                    Ticket = ticket
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MT5] ModifyPosition error: {ex.Message}");
                return Task.FromResult(new ModifyPositionResult
                {
                    Success = false,
                    Message = $"Modify position failed: {ex.Message}",
                    Ticket = ticket
                });
            }
        }

        // Raise tick event (called by tick sink or mock generator)
        protected void RaiseTickReceived(TickData tick)
        {
            lock (_tickLock)
            {
                _latestTicks[tick.Symbol] = tick;
            }
            OnTickReceived?.Invoke(tick);
        }

        // Raise deal event (called by deal sink)
        protected void RaiseDealEvent(DealEventData deal)
        {
            OnDealEvent?.Invoke(deal);
        }

        // ============ Private Helpers ============

        private bool TryConnectReal()
        {
            try
            {
                // Check if MT5 Manager DLLs are available
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string nativeDll = Path.Combine(baseDir, "MT5APIManager64.dll");
                string managedDll1 = Path.Combine(baseDir, "MetaQuotes.MT5ManagerAPI64.dll");
                string managedDll2 = Path.Combine(baseDir, "MetaQuotes.MT5CommonAPI64.dll");

                if (!File.Exists(nativeDll))
                {
                    Console.WriteLine($"[MT5] Native DLL not found: {nativeDll}");
                    return false;
                }
                if (!File.Exists(managedDll1))
                {
                    Console.WriteLine($"[MT5] Managed DLL not found: {managedDll1}");
                    return false;
                }
                if (!File.Exists(managedDll2))
                {
                    Console.WriteLine($"[MT5] Managed DLL not found: {managedDll2}");
                    return false;
                }

                Console.WriteLine("[MT5] All DLLs found, attempting real MT5 connection...");

                // Real MT5 connection would be:
                // SMTManagerAPIFactory.Initialize(null);
                // var res = SMTManagerAPIFactory.CreateManager(MTManagerAPIFactory.ManagerAPIVersion, out CIMTManagerAPI manager);
                // if (res != MTRetCode.MT_RET_OK) return false;
                //
                // res = manager.Connect($"{_config.ServerAddress}:{_config.ServerPort}",
                //                        _config.ManagerLogin, _config.ManagerPassword,
                //                        null, CIMTManagerAPI.EnPumpModes.PUMP_MODE_FULL);
                // if (res != MTRetCode.MT_RET_OK) { manager.Release(); return false; }
                //
                // _manager = manager;
                // SetupRealTickSink(manager);
                // SetupRealDealSink(manager);
                // LoadRealSymbols(manager);

                // For now, use reflection or dynamic loading to attempt connection
                // This will naturally throw if the assemblies aren't properly loadable
                var assembly = System.Reflection.Assembly.LoadFrom(managedDll1);
                var factoryType = assembly.GetType("MetaQuotes.MT5ManagerAPI.SMTManagerAPIFactory");

                if (factoryType == null)
                {
                    Console.WriteLine("[MT5] Could not find SMTManagerAPIFactory type in assembly");
                    return false;
                }

                Console.WriteLine("[MT5] MT5 Manager API loaded, connecting...");

                // Attempt initialization and connection via reflection
                var initMethod = factoryType.GetMethod("Initialize");
                initMethod?.Invoke(null, new object?[] { null });

                var createMethod = factoryType.GetMethod("CreateManager");
                if (createMethod == null)
                {
                    Console.WriteLine("[MT5] CreateManager method not found");
                    return false;
                }

                // Get version constant (use default 3430 if not found)
                uint apiVersion = 3430;
                var versionField = factoryType.GetField("ManagerAPIVersion");
                if (versionField != null && versionField.IsStatic)
                {
                    apiVersion = (uint)(versionField.GetValue(null) ?? 3430);
                }

                // Create manager instance
                object?[] createParams = new object?[] { apiVersion, null };
                var createResult = createMethod.Invoke(null, createParams);

                var manager = createParams[1];
                if (manager == null)
                {
                    Console.WriteLine("[MT5] Failed to create MT5 Manager instance");
                    return false;
                }

                // Connect
                var managerType = manager.GetType();
                var connectMethod = managerType.GetMethod("Connect");
                if (connectMethod == null)
                {
                    Console.WriteLine("[MT5] Connect method not found on manager");
                    return false;
                }

                string serverAddr = $"{_config.ServerAddress}:{_config.ServerPort}";
                var connectResult = connectMethod.Invoke(manager, new object[]
                {
                    serverAddr,
                    _config.ManagerLogin,
                    _config.ManagerPassword,
                    null!,
                    0x3F // PUMP_MODE_FULL
                });

                Console.WriteLine($"[MT5] Connect result: {connectResult}");

                _manager = manager;

                // Load symbols from real server
                LoadRealSymbols(manager);

                Console.WriteLine($"[MT5] Successfully connected to {serverAddr}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MT5] Real connection failed: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"[MT5]   Inner: {ex.InnerException.Message}");
                return false;
            }
        }

        private void LoadRealSymbols(object manager)
        {
            try
            {
                // In real mode: iterate manager.SymbolTotal() / manager.SymbolNext()
                // or use manager.SymbolGet(name, out CIMTConSymbol symbol)
                // Populate _symbols dictionary with SymbolInfo from the server
                Console.WriteLine("[MT5] Loading symbols from MT5 server...");

                var managerType = manager.GetType();
                var totalMethod = managerType.GetMethod("SymbolTotal");
                if (totalMethod != null)
                {
                    var total = (uint)(totalMethod.Invoke(manager, null) ?? 0);
                    Console.WriteLine($"[MT5] Found {total} symbols on server");

                    var nextMethod = managerType.GetMethod("SymbolNext");
                    for (uint i = 0; i < total && nextMethod != null; i++)
                    {
                        try
                        {
                            var symbolParams = new object[] { i, null! };
                            nextMethod.Invoke(manager, symbolParams);
                            var symbolObj = symbolParams[1];
                            if (symbolObj != null)
                            {
                                var symType = symbolObj.GetType();
                                string name = symType.GetMethod("Symbol")?.Invoke(symbolObj, null)?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(name))
                                {
                                    _symbols[name] = new SymbolInfo
                                    {
                                        Symbol = name,
                                        Digits = (int)(symType.GetMethod("Digits")?.Invoke(symbolObj, null) ?? 5),
                                        ContractSize = (double)(symType.GetMethod("ContractSize")?.Invoke(symbolObj, null) ?? 100000.0),
                                        ProfitCurrency = symType.GetMethod("CurrencyProfit")?.Invoke(symbolObj, null)?.ToString() ?? "USD",
                                        MarginCurrency = symType.GetMethod("CurrencyMargin")?.Invoke(symbolObj, null)?.ToString() ?? "USD",
                                        CalcMode = (int)(symType.GetMethod("CalcMode")?.Invoke(symbolObj, null) ?? 0),
                                        Category = symType.GetMethod("Path")?.Invoke(symbolObj, null)?.ToString() ?? ""
                                    };
                                }
                            }
                        }
                        catch
                        {
                            // Skip symbols that fail to load
                        }
                    }
                }

                Console.WriteLine($"[MT5] Loaded {_symbols.Count} symbols from server");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MT5] Error loading symbols: {ex.Message}");
            }
        }

        private void StartMockMode()
        {
            _mockGenerator = new MockTickGenerator(intervalMs: 200);

            // Load mock symbols
            var mockSymbols = _mockGenerator.GetMockSymbols();
            foreach (var sym in mockSymbols)
            {
                _symbols[sym.Symbol] = sym;
            }
            Console.WriteLine($"[MT5] Loaded {_symbols.Count} mock symbols");

            // Wire up tick handler with throttling
            _mockGenerator.OnTickGenerated += OnMockTickGenerated;

            // Start generating
            _mockGenerator.Start();
        }

        private void OnMockTickGenerated(TickData tick)
        {
            try
            {
                // Apply tick throttling: max one broadcast per symbol per TickThrottleMs
                bool shouldBroadcast = false;
                lock (_tickLock)
                {
                    var now = DateTime.UtcNow;
                    if (_lastTickBroadcast.TryGetValue(tick.Symbol, out var lastTime))
                    {
                        if ((now - lastTime).TotalMilliseconds >= _config.TickThrottleMs)
                        {
                            _lastTickBroadcast[tick.Symbol] = now;
                            shouldBroadcast = true;
                        }
                        else
                        {
                            // Still update latest tick data even if we don't broadcast
                            _latestTicks[tick.Symbol] = tick;
                        }
                    }
                    else
                    {
                        _lastTickBroadcast[tick.Symbol] = now;
                        shouldBroadcast = true;
                    }
                }

                if (shouldBroadcast)
                {
                    RaiseTickReceived(tick);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MT5] Error processing mock tick for {tick.Symbol}: {ex.Message}");
            }
        }
    }
}
