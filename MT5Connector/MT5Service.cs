using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading.Channels;

namespace MT5Connector
{
    public class MT5Service
    {
        public event Action<TickData>? OnTickReceived;
        public event Action<DealEventData>? OnDealEvent;

        private readonly MT5Config _config;
        private readonly Dictionary<string, TickData> _latestTicks = new();
        private readonly ConcurrentDictionary<string, SymbolInfo> _symbols = new();
        private bool _connected = false;

        // Mock mode
        private MockTickGenerator? _mockGenerator;
        private bool _isMockMode = false;

        // Tick throttling: track last broadcast time per symbol
        private readonly Dictionary<string, DateTime> _lastTickBroadcast = new();
        private readonly object _tickLock = new();

        // MT5 Manager API handle (only used in real mode)
        private object? _manager;
        private CancellationTokenSource? _tickPumpCts;
        private Task? _tickPumpTask;

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
                StartRealTickPump();
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

            if (!_isMockMode)
            {
                // Stop tick pump
                if (_tickPumpCts != null)
                {
                    _tickPumpCts.Cancel();
                    try { _tickPumpTask?.Wait(3000); } catch { }
                    _tickPumpCts.Dispose();
                    _tickPumpCts = null;
                    _tickPumpTask = null;
                    Console.WriteLine("[MT5] Tick pump stopped");
                }

                if (_manager != null)
                {
                    try
                    {
                        var disconnectMethod = _manager.GetType().GetMethod("Disconnect");
                        disconnectMethod?.Invoke(_manager, null);
                        var releaseMethod = _manager.GetType().GetMethod("Release");
                        releaseMethod?.Invoke(_manager, null);
                        Console.WriteLine("[MT5] Real MT5 connection closed");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MT5] Error during disconnect: {ex.Message}");
                    }
                    _manager = null;
                }
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

            // Real MT5 trade operations not yet implemented
            Console.WriteLine($"[MT5] ClosePosition: login={login}, ticket={ticket}, symbol={symbol} — NOT IMPLEMENTED");
            return Task.FromResult(new ClosePositionResult
            {
                Success = false,
                Message = "Real mode trading not yet implemented",
                Ticket = ticket
            });
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

            // Real MT5 trade operations not yet implemented
            Console.WriteLine($"[MT5] OpenPosition: login={login}, symbol={symbol} — NOT IMPLEMENTED");
            return Task.FromResult(new OpenPositionResult
            {
                Success = false,
                Message = "Real mode trading not yet implemented",
                Ticket = 0
            });
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

            // Real MT5 trade operations not yet implemented
            Console.WriteLine($"[MT5] ModifyPosition: login={login}, ticket={ticket}, symbol={symbol} — NOT IMPLEMENTED");
            return Task.FromResult(new ModifyPositionResult
            {
                Success = false,
                Message = "Real mode trading not yet implemented",
                Ticket = ticket
            });
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

                // Initialize the MT5 Manager API with DLL path
                var initMethod = factoryType.GetMethod("Initialize");
                if (initMethod != null)
                {
                    var initParams = initMethod.GetParameters();
                    try
                    {
                        if (initParams.Length == 1 && initParams[0].ParameterType == typeof(string))
                            initMethod.Invoke(null, new object?[] { baseDir });
                        else
                            initMethod.Invoke(null, new object?[] { null });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MT5] Initialize error: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }

                // Get version constant (use default 3430 if not found)
                uint apiVersion = 3430;
                var versionField = factoryType.GetField("ManagerAPIVersion");
                if (versionField != null && versionField.IsStatic)
                {
                    apiVersion = (uint)(versionField.GetValue(null) ?? 3430);
                }

                // Find CreateManager overloads - prefer 3-param with data_path, fallback to 2-param
                MethodInfo? createMethod;
                var allCreateMethods = factoryType.GetMethods()
                    .Where(m => m.Name == "CreateManager")
                    .OrderByDescending(m => m.GetParameters().Length) // try 3-param first
                    .ToList();

                object?[] createParams;

                // Try the 3-param version first: CreateManager(uint version, string data_path, MTRetCode& res) -> CIMTManagerAPI
                var create3 = allCreateMethods.FirstOrDefault(m => m.GetParameters().Length == 3);
                var create2 = allCreateMethods.FirstOrDefault(m => m.GetParameters().Length == 2);

                if (create3 != null)
                {
                    createMethod = create3;
                    createParams = new object?[] { apiVersion, baseDir, null };
                }
                else if (create2 != null)
                {
                    createMethod = create2;
                    createParams = new object?[] { apiVersion, null };
                }
                else
                {
                    Console.WriteLine("[MT5] No suitable CreateManager overload found");
                    return false;
                }

                var createResult = createMethod.Invoke(null, createParams);

                // The manager could be the return value or an out param
                object? manager = null;
                // Check return value first
                if (createResult != null && createResult.GetType().Name.Contains("Manager"))
                {
                    manager = createResult;
                }
                // Check out params
                for (int i = 0; i < createParams.Length; i++)
                {
                    if (createParams[i] != null && createParams[i]!.GetType().Name.Contains("Manager"))
                    {
                        manager = createParams[i];
                        break;
                    }
                }
                // If still no manager, check if return value has Connect method
                if (manager == null && createResult != null && createResult.GetType().GetMethod("Connect") != null)
                {
                    manager = createResult;
                }

                if (manager == null)
                {
                    Console.WriteLine("[MT5] Failed to create MT5 Manager instance");
                    return false;
                }

                // Connect via reflection
                var managerType = manager.GetType();
                var connectMethods = managerType.GetMethods().Where(m => m.Name == "Connect").ToList();

                string serverAddr = $"{_config.ServerAddress}:{_config.ServerPort}";
                object? connectResult = null;

                // Build Connect args dynamically based on discovered signature
                foreach (var cm in connectMethods)
                {
                    var pars = cm.GetParameters();
                    if (pars.Length < 4) continue;

                    try
                    {
                        var connectArgs = new List<object>();
                        foreach (var p in pars)
                        {
                            var pName = p.Name?.ToLower() ?? "";
                            var pType = p.ParameterType;

                            if (pName.Contains("server"))
                                connectArgs.Add(serverAddr);
                            else if (pName.Contains("login"))
                                connectArgs.Add((ulong)_config.ManagerLogin);
                            else if (pName.Contains("password") && !pName.Contains("cert"))
                                connectArgs.Add(_config.ManagerPassword);
                            else if (pName.Contains("cert") || pName.Contains("password_cert"))
                                connectArgs.Add("");
                            else if (pName.Contains("pump") || pName.Contains("mode"))
                            {
                                if (pType.IsEnum)
                                    connectArgs.Add(Enum.ToObject(pType, 0x3FF));
                                else
                                    connectArgs.Add((uint)0x3FF);
                            }
                            else if (pName.Contains("timeout"))
                                connectArgs.Add((uint)30000);
                            else if (pType == typeof(string))
                                connectArgs.Add("");
                            else if (pType == typeof(uint))
                                connectArgs.Add((uint)0);
                            else if (pType == typeof(ulong))
                                connectArgs.Add((ulong)0);
                            else
                                connectArgs.Add(pType.IsValueType ? Activator.CreateInstance(pType)! : null!);
                        }

                        connectResult = cm.Invoke(manager, connectArgs.ToArray());
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MT5] Connect overload failed: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }

                // Check if connect succeeded — MT_RET_OK is typically enum value 0
                if (connectResult == null)
                {
                    Console.WriteLine($"[MT5] Connection to {serverAddr} failed: no result");
                    return false;
                }
                var connectResultStr = connectResult.ToString() ?? "";
                bool connectOk = connectResultStr == "MT_RET_OK"
                    || (connectResult.GetType().IsEnum && Convert.ToInt32(connectResult) == 0);
                if (!connectOk)
                {
                    Console.WriteLine($"[MT5] Connection to {serverAddr} failed: {connectResultStr}");
                    return false;
                }

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

                    // SymbolNext may need a CIMTConSymbol object created first
                    // First, find SymbolCreate to make the symbol object
                    var symbolCreateMethod = managerType.GetMethod("SymbolCreate");
                    object? symbolTemplate = null;
                    if (symbolCreateMethod != null)
                    {
                        var scParams = symbolCreateMethod.GetParameters();
                        if (scParams.Length == 1) // out param
                        {
                            var scArgs = new object?[] { null };
                            symbolCreateMethod.Invoke(manager, scArgs);
                            symbolTemplate = scArgs[0];
                        }
                        else if (scParams.Length == 0)
                        {
                            symbolTemplate = symbolCreateMethod.Invoke(manager, null);
                        }
                    }

                    var nextMethod = managerType.GetMethod("SymbolNext");
                    if (nextMethod != null)
                    {
                        var nextParams = nextMethod.GetParameters();

                        for (uint i = 0; i < total; i++)
                        {
                            try
                            {
                                object? symbolObj = null;
                                object? retCode = null;

                                if (nextParams.Length == 2 && nextParams[0].ParameterType == typeof(uint))
                                {
                                    // SymbolNext(uint pos, CIMTConSymbol symbol) - pass the template
                                    if (symbolTemplate == null) break;
                                    var args = new object[] { i, symbolTemplate };
                                    retCode = nextMethod.Invoke(manager, args);
                                    symbolObj = args[1]; // might be updated in place
                                }
                                else if (nextParams.Length == 1)
                                {
                                    var args = new object?[] { null };
                                    retCode = nextMethod.Invoke(manager, args);
                                    symbolObj = args[0];
                                }

                                if (symbolObj == null) continue;

                                var symType = symbolObj.GetType();

                                // Get symbol name via cached reflection helper
                                string name = GetReflectionValue(symType, symbolObj, "Symbol")?.ToString() ?? "";
                                if (string.IsNullOrEmpty(name))
                                    name = GetReflectionValue(symType, symbolObj, "Name")?.ToString() ?? "";

                                if (!string.IsNullOrEmpty(name))
                                {
                                    int digits = 5;
                                    try { digits = Convert.ToInt32(GetReflectionValue(symType, symbolObj, "Digits") ?? 5); } catch { }

                                    double contractSize = 100000.0;
                                    try { contractSize = Convert.ToDouble(GetReflectionValue(symType, symbolObj, "ContractSize") ?? 100000.0); } catch { }

                                    string category = GetReflectionValue(symType, symbolObj, "Path")?.ToString() ?? "";
                                    string profitCurrency = GetReflectionValue(symType, symbolObj, "CurrencyProfit")?.ToString() ?? "USD";
                                    string marginCurrency = GetReflectionValue(symType, symbolObj, "CurrencyMargin")?.ToString() ?? "USD";

                                    _symbols[name] = new SymbolInfo
                                    {
                                        Symbol = name,
                                        Digits = digits,
                                        ContractSize = contractSize,
                                        ProfitCurrency = profitCurrency,
                                        MarginCurrency = marginCurrency,
                                        Category = category
                                    };
                                }
                            }
                            catch
                            {
                                // Skip symbols that fail to load
                            }
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

        // Reflection cache: avoids repeated GetMethods() scanning on hot paths
        private static readonly ConcurrentDictionary<(Type, string), MethodInfo?> _reflectionCache = new();

        // Helper: safely get a parameterless method/property value via reflection (avoids ambiguity)
        private static object? GetReflectionValue(Type type, object obj, string name)
        {
            try
            {
                var key = (type, name);
                if (!_reflectionCache.TryGetValue(key, out var method))
                {
                    method = type.GetMethods().FirstOrDefault(m => m.Name == name && m.GetParameters().Length == 0);
                    _reflectionCache[key] = method;
                }
                if (method != null) return method.Invoke(obj, null);

                // Fallback: try property
                var prop = type.GetProperty(name);
                if (prop != null) return prop.GetValue(obj);
            }
            catch { }
            return null;
        }

        // Shared tick construction: builds TickData with spread and direction
        private TickData BuildTickData(string symbol, double bid, double ask, double volume = 0)
        {
            var info = GetSymbolInfo(symbol);
            int digits = info?.Digits ?? 5;

            var tickData = new TickData
            {
                Symbol = symbol,
                Bid = bid,
                Ask = ask,
                Volume = volume,
                Digits = digits,
                TickTime = DateTime.UtcNow
            };

            if (bid > 0 && ask > 0)
            {
                double pipMultiplier = Math.Pow(10, digits);
                tickData.Spread = Math.Round((ask - bid) * pipMultiplier, 1);
            }

            // Compute direction from previous tick
            TickData? prev;
            lock (_tickLock) { _latestTicks.TryGetValue(symbol, out prev); }
            if (prev != null)
                tickData.Direction = bid > prev.Bid ? "up" : bid < prev.Bid ? "down" : "none";

            return tickData;
        }

        private void StartRealTickPump()
        {
            if (_manager == null) return;

            _tickPumpCts = new CancellationTokenSource();
            var ct = _tickPumpCts.Token;
            var manager = _manager;
            var managerType = manager.GetType();

            // Try to find batch TickLast(UInt32& id, MTRetCode& res) -> MTTick[]
            var batchTickMethod = managerType.GetMethods()
                .FirstOrDefault(m => m.Name == "TickLast" &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType == typeof(uint).MakeByRefType());

            // Find per-symbol TickLast(String symbol, MTTickShort& tick)
            var perSymbolTickMethod = managerType.GetMethods()
                .FirstOrDefault(m => m.Name == "TickLast" &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType == typeof(string));

            Console.WriteLine($"[MT5] Tick pump: batch method={batchTickMethod != null}, per-symbol method={perSymbolTickMethod != null}");

            _tickPumpTask = Task.Run(async () =>
            {
                uint lastTickId = 0;
                int pollIntervalMs = _config.TickThrottleMs > 0 ? _config.TickThrottleMs : 200;
                int symbolPollIntervalMs = 5000; // Poll individual symbols every 5s
                DateTime lastSymbolPoll = DateTime.MinValue;

                Console.WriteLine($"[MT5] Tick pump started (interval={pollIntervalMs}ms)");

                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        int ticksProcessed = 0;

                        // Strategy 1: Batch tick polling
                        if (batchTickMethod != null)
                        {
                            try
                            {
                                object?[] batchParams = new object?[] { lastTickId, null };
                                var result = batchTickMethod.Invoke(manager, batchParams);

                                if (result is Array tickArray && tickArray.Length > 0)
                                {
                                    lastTickId = (uint)(batchParams[0] ?? lastTickId);

                                    foreach (var mtTick in tickArray)
                                    {
                                        var tickType = mtTick.GetType();
                                        string symbol = GetReflectionValue(tickType, mtTick, "Symbol")?.ToString() ?? "";
                                        if (string.IsNullOrEmpty(symbol)) continue;

                                        double bid = Convert.ToDouble(GetReflectionValue(tickType, mtTick, "Bid") ?? 0);
                                        double ask = Convert.ToDouble(GetReflectionValue(tickType, mtTick, "Ask") ?? 0);
                                        double last = Convert.ToDouble(GetReflectionValue(tickType, mtTick, "Last") ?? 0);
                                        ulong volume = Convert.ToUInt64(GetReflectionValue(tickType, mtTick, "Volume") ?? 0UL);

                                        if (bid <= 0 && ask <= 0 && last <= 0) continue;

                                        var tickData = BuildTickData(symbol, bid, ask, (double)volume);
                                        RaiseTickReceived(tickData);
                                        ticksProcessed++;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[MT5] Batch tick error: {ex.InnerException?.Message ?? ex.Message}");
                            }
                        }

                        // Strategy 2: Per-symbol polling (fallback/supplement, less frequent)
                        if (perSymbolTickMethod != null && (DateTime.UtcNow - lastSymbolPoll).TotalMilliseconds >= symbolPollIntervalMs)
                        {
                            lastSymbolPoll = DateTime.UtcNow;
                            var symbolNames = _symbols.Keys.ToList();
                            int perSymbolTicks = 0;

                            foreach (var symbol in symbolNames)
                            {
                                if (ct.IsCancellationRequested) break;

                                try
                                {
                                    object?[] symParams = new object?[] { symbol, null };
                                    var retCode = perSymbolTickMethod.Invoke(manager, symParams);
                                    var tickShort = symParams[1];

                                    if (tickShort == null) continue;

                                    var tType = tickShort.GetType();
                                    double bid = Convert.ToDouble(GetReflectionValue(tType, tickShort, "Bid") ?? 0);
                                    double ask = Convert.ToDouble(GetReflectionValue(tType, tickShort, "Ask") ?? 0);

                                    if (bid <= 0 && ask <= 0) continue;

                                    // Only update if we don't already have this price
                                    TickData? existing;
                                    lock (_tickLock)
                                    {
                                        _latestTicks.TryGetValue(symbol, out existing);
                                    }

                                    if (existing != null && Math.Abs(existing.Bid - bid) < 1e-10 && Math.Abs(existing.Ask - ask) < 1e-10)
                                        continue;

                                    var tickData = BuildTickData(symbol, bid, ask);
                                    RaiseTickReceived(tickData);
                                    perSymbolTicks++;
                                }
                                catch
                                {
                                    // Skip symbols that fail
                                }
                            }

                            if (perSymbolTicks > 0)
                                Console.WriteLine($"[MT5] Per-symbol poll: {perSymbolTicks} symbols with prices");
                        }

                        await Task.Delay(pollIntervalMs, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MT5] Tick pump error: {ex.Message}");
                        await Task.Delay(1000, ct);
                    }
                }

                Console.WriteLine("[MT5] Tick pump stopped");
            }, ct);
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
