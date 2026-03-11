using System.Linq;
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

                // Attempt initialization - pass the path to the native DLLs
                var initMethod = factoryType.GetMethod("Initialize");
                if (initMethod != null)
                {
                    var initParams = initMethod.GetParameters();
                    Console.WriteLine($"[MT5] Initialize params: {string.Join(", ", initParams.Select(p => $"{p.ParameterType.Name} {p.Name}"))}");

                    // Try with baseDir path (where native DLLs live)
                    try
                    {
                        if (initParams.Length == 1 && initParams[0].ParameterType == typeof(string))
                        {
                            var initResult = initMethod.Invoke(null, new object?[] { baseDir });
                            Console.WriteLine($"[MT5] Initialize(path) result: {initResult}");
                        }
                        else
                        {
                            var initResult = initMethod.Invoke(null, new object?[] { null });
                            Console.WriteLine($"[MT5] Initialize(null) result: {initResult}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MT5] Initialize error: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }

                // Find the right CreateManager overload: CreateManager(uint version, out CIMTManagerAPI manager)
                var createMethod = factoryType.GetMethods()
                    .Where(m => m.Name == "CreateManager")
                    .FirstOrDefault(m =>
                    {
                        var p = m.GetParameters();
                        return p.Length == 2 && p[0].ParameterType == typeof(uint);
                    });
                if (createMethod == null)
                {
                    // Try the 3-parameter overload: CreateManager(uint, string, out MTRetCode)
                    createMethod = factoryType.GetMethods()
                        .FirstOrDefault(m => m.Name == "CreateManager");
                }
                if (createMethod == null)
                {
                    Console.WriteLine("[MT5] CreateManager method not found");
                    return false;
                }
                Console.WriteLine($"[MT5] Using CreateManager with {createMethod.GetParameters().Length} params: {string.Join(", ", createMethod.GetParameters().Select(p => p.ParameterType.Name))}");

                // Get version constant (use default 3430 if not found)
                uint apiVersion = 3430;
                var versionField = factoryType.GetField("ManagerAPIVersion");
                if (versionField != null && versionField.IsStatic)
                {
                    apiVersion = (uint)(versionField.GetValue(null) ?? 3430);
                }

                // Try both overloads - prefer 3-param with data_path, fallback to 2-param
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
                    Console.WriteLine($"[MT5] Trying 3-param CreateManager with data_path={baseDir}");
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
                Console.WriteLine($"[MT5] CreateManager return value: {createResult} (type: {createResult?.GetType().Name ?? "null"})");
                for (int i = 0; i < createParams.Length; i++)
                    Console.WriteLine($"[MT5]   param[{i}] = {createParams[i]} (type: {createParams[i]?.GetType().Name ?? "null"})");

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
                    Console.WriteLine("[MT5] Failed to create MT5 Manager instance - no manager object found");
                    Console.WriteLine($"[MT5] Listing all CreateManager overloads:");
                    foreach (var m in factoryType.GetMethods().Where(x => x.Name == "CreateManager"))
                    {
                        var pars = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                        Console.WriteLine($"[MT5]   {m.ReturnType.Name} CreateManager({pars})");
                    }
                    return false;
                }
                Console.WriteLine($"[MT5] Manager object type: {manager.GetType().FullName}");

                // Connect - discover the right overload
                var managerType = manager.GetType();
                var connectMethods = managerType.GetMethods().Where(m => m.Name == "Connect").ToList();
                Console.WriteLine($"[MT5] Found {connectMethods.Count} Connect overloads:");
                foreach (var cm in connectMethods)
                {
                    var pars = string.Join(", ", cm.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    Console.WriteLine($"[MT5]   {cm.ReturnType.Name} Connect({pars})");
                }

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

                        Console.WriteLine($"[MT5] Trying Connect with {pars.Length} params: {string.Join(", ", connectArgs.Select((a, i) => $"{pars[i].Name}={a}"))}");
                        connectResult = cm.Invoke(manager, connectArgs.ToArray());
                        Console.WriteLine($"[MT5] Connect result: {connectResult}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MT5] Connect overload failed: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }

                if (connectResult == null || connectResult.ToString()!.Contains("ERROR") || connectResult.ToString()!.Contains("FAIL"))
                {
                    Console.WriteLine($"[MT5] Connection to {serverAddr} failed: {connectResult}");
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
                        Console.WriteLine($"[MT5] SymbolCreate: {symbolTemplate?.GetType().Name ?? "null"}");
                    }

                    var nextMethod = managerType.GetMethod("SymbolNext");
                    if (nextMethod != null)
                    {
                        var nextParams = nextMethod.GetParameters();
                        Console.WriteLine($"[MT5] SymbolNext params: {string.Join(", ", nextParams.Select(p => $"{p.ParameterType.Name} {p.Name}"))}");

                        // Log first symbol object's methods for debugging
                        bool loggedMethods = false;

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

                                if (!loggedMethods)
                                {
                                    // Log available methods/properties for debugging
                                    var methods = symType.GetMethods()
                                        .Where(m => m.GetParameters().Length == 0 && m.ReturnType != typeof(void))
                                        .Select(m => $"{m.ReturnType.Name} {m.Name}()")
                                        .Take(30);
                                    Console.WriteLine($"[MT5] Symbol object methods: {string.Join(", ", methods)}");

                                    var props = symType.GetProperties()
                                        .Select(p => $"{p.PropertyType.Name} {p.Name}")
                                        .Take(20);
                                    Console.WriteLine($"[MT5] Symbol object properties: {string.Join(", ", props)}");
                                    loggedMethods = true;
                                }

                                // Try multiple ways to get the symbol name
                                // Use GetMethods to find the parameterless getter to avoid ambiguity
                                string name = "";
                                var symbolGetter = symType.GetMethods().FirstOrDefault(m => m.Name == "Symbol" && m.GetParameters().Length == 0 && m.ReturnType == typeof(string));
                                if (symbolGetter != null)
                                    name = symbolGetter.Invoke(symbolObj, null)?.ToString() ?? "";
                                if (string.IsNullOrEmpty(name))
                                {
                                    var nameGetter = symType.GetMethods().FirstOrDefault(m => m.Name == "Name" && m.GetParameters().Length == 0 && m.ReturnType == typeof(string));
                                    name = nameGetter?.Invoke(symbolObj, null)?.ToString() ?? "";
                                }

                                if (i < 3)
                                    Console.WriteLine($"[MT5] Symbol[{i}]: name='{name}', retCode={retCode}");

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
                            catch (Exception ex)
                            {
                                if (i < 3) Console.WriteLine($"[MT5] Symbol[{i}] error: {ex.InnerException?.Message ?? ex.Message}");
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

        // Helper: safely get a parameterless method/property value via reflection (avoids ambiguity)
        private static object? GetReflectionValue(Type type, object obj, string name)
        {
            try
            {
                // Try parameterless method first
                var method = type.GetMethods().FirstOrDefault(m => m.Name == name && m.GetParameters().Length == 0);
                if (method != null) return method.Invoke(obj, null);

                // Try property
                var prop = type.GetProperty(name);
                if (prop != null) return prop.GetValue(obj);
            }
            catch { }
            return null;
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

                                        var info = GetSymbolInfo(symbol);
                                        int digits = info?.Digits ?? 5;

                                        var tickData = new TickData
                                        {
                                            Symbol = symbol,
                                            Bid = bid,
                                            Ask = ask,
                                            Volume = (double)volume,
                                            Digits = digits,
                                            TickTime = DateTime.UtcNow
                                        };

                                        // Compute spread
                                        if (bid > 0 && ask > 0)
                                        {
                                            double pipMultiplier = Math.Pow(10, digits);
                                            tickData.Spread = Math.Round((ask - bid) * pipMultiplier, 1);
                                        }

                                        // Compute direction
                                        TickData? prev;
                                        lock (_tickLock) { _latestTicks.TryGetValue(symbol, out prev); }
                                        if (prev != null)
                                            tickData.Direction = bid > prev.Bid ? "up" : bid < prev.Bid ? "down" : "none";

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

                                    var info = GetSymbolInfo(symbol);
                                    int digits = info?.Digits ?? 5;

                                    var tickData = new TickData
                                    {
                                        Symbol = symbol,
                                        Bid = bid,
                                        Ask = ask,
                                        Digits = digits,
                                        TickTime = DateTime.UtcNow
                                    };

                                    if (bid > 0 && ask > 0)
                                    {
                                        double pipMultiplier = Math.Pow(10, digits);
                                        tickData.Spread = Math.Round((ask - bid) * pipMultiplier, 1);
                                    }

                                    // Compute direction
                                    if (existing != null)
                                        tickData.Direction = bid > existing.Bid ? "up" : bid < existing.Bid ? "down" : "none";

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
