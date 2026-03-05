namespace MT5Connector
{
    public class MockTickGenerator
    {
        public event Action<TickData>? OnTickGenerated;

        private Timer? _timer;
        private readonly int _intervalMs;
        private readonly Random _random = new();
        private readonly object _lock = new();

        // Current state per symbol
        private readonly Dictionary<string, double> _currentBid = new();
        private readonly Dictionary<string, double> _previousBid = new();
        private readonly Dictionary<string, double> _sessionHigh = new();
        private readonly Dictionary<string, double> _sessionLow = new();

        // Symbol definitions: (symbol, baseBid, spreadPips, digits, contractSize, profitCurrency, marginCurrency, calcMode, category)
        private static readonly List<SymbolDef> SymbolDefs = new()
        {
            // Forex Majors
            new("EURUSD",  1.0830,  1.2, 5, 100000, "USD", "EUR", 0, "Forex Majors"),
            new("GBPUSD",  1.2640,  1.5, 5, 100000, "USD", "GBP", 0, "Forex Majors"),
            new("USDJPY",  154.50,  1.3, 3, 100000, "JPY", "USD", 0, "Forex Majors"),
            new("USDCHF",  0.8820,  1.5, 5, 100000, "CHF", "USD", 0, "Forex Majors"),
            new("AUDUSD",  0.6530,  1.4, 5, 100000, "USD", "AUD", 0, "Forex Majors"),
            new("USDCAD",  1.3620,  1.6, 5, 100000, "CAD", "USD", 0, "Forex Majors"),
            new("NZDUSD",  0.6080,  1.8, 5, 100000, "USD", "NZD", 0, "Forex Majors"),

            // Forex Crosses
            new("EURGBP",  0.8570,  1.8, 5, 100000, "GBP", "EUR", 0, "Forex Crosses"),
            new("EURJPY",  167.40,  2.0, 3, 100000, "JPY", "EUR", 0, "Forex Crosses"),
            new("GBPJPY",  195.30,  2.5, 3, 100000, "JPY", "GBP", 0, "Forex Crosses"),
            new("EURCHF",  0.9560,  2.0, 5, 100000, "CHF", "EUR", 0, "Forex Crosses"),
            new("AUDNZD",  1.0740,  2.5, 5, 100000, "NZD", "AUD", 0, "Forex Crosses"),
            new("EURAUD",  1.6590,  2.5, 5, 100000, "AUD", "EUR", 0, "Forex Crosses"),
            new("GBPAUD",  1.9360,  3.0, 5, 100000, "AUD", "GBP", 0, "Forex Crosses"),
            new("AUDCAD",  0.8900,  2.2, 5, 100000, "CAD", "AUD", 0, "Forex Crosses"),
            new("CADJPY",  113.50,  2.0, 3, 100000, "JPY", "CAD", 0, "Forex Crosses"),
            new("CHFJPY",  175.20,  2.5, 3, 100000, "JPY", "CHF", 0, "Forex Crosses"),

            // Metals
            new("XAUUSD",  2350.00, 30.0, 2, 100, "USD", "USD", 0, "Metals"),
            new("XAGUSD",  28.50,   5.0,  3, 5000, "USD", "USD", 0, "Metals"),

            // Indices
            new("US30",    39200.0, 200.0, 2, 1, "USD", "USD", 4, "Indices"),
            new("NAS100",  18100.0, 150.0, 2, 1, "USD", "USD", 4, "Indices"),
            new("SPX500",  5180.0,  50.0,  2, 1, "USD", "USD", 4, "Indices"),
            new("UK100",   8050.0,  150.0, 2, 1, "GBP", "GBP", 4, "Indices"),
            new("GER40",   18350.0, 200.0, 2, 1, "EUR", "EUR", 4, "Indices"),

            // Crypto
            new("BTCUSD",  65000.0, 3000.0, 2, 1, "USD", "USD", 0, "Crypto"),
            new("ETHUSD",  3400.0,  200.0,  2, 1, "USD", "USD", 0, "Crypto"),
        };

        public MockTickGenerator(int intervalMs = 200)
        {
            _intervalMs = intervalMs;
            InitializePrices();
        }

        private void InitializePrices()
        {
            foreach (var def in SymbolDefs)
            {
                _currentBid[def.Symbol] = def.BaseBid;
                _previousBid[def.Symbol] = def.BaseBid;
                _sessionHigh[def.Symbol] = def.BaseBid;
                _sessionLow[def.Symbol] = def.BaseBid;
            }
        }

        public void Start()
        {
            if (_timer != null) return;

            Console.WriteLine($"[MT5] MockTickGenerator started with {SymbolDefs.Count} symbols, interval={_intervalMs}ms");
            _timer = new Timer(GenerateTicks, null, 0, _intervalMs);
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
            Console.WriteLine("[MT5] MockTickGenerator stopped");
        }

        public List<SymbolInfo> GetMockSymbols()
        {
            return SymbolDefs.Select(d => new SymbolInfo
            {
                Symbol = d.Symbol,
                Digits = d.Digits,
                ContractSize = d.ContractSize,
                ProfitCurrency = d.ProfitCurrency,
                MarginCurrency = d.MarginCurrency,
                CalcMode = d.CalcMode,
                Category = d.Category
            }).ToList();
        }

        private void GenerateTicks(object? state)
        {
            try
            {
                // Each tick cycle, generate ticks for a random subset of symbols
                // to simulate realistic market activity (not all symbols tick simultaneously)
                int count = _random.Next(3, Math.Min(10, SymbolDefs.Count + 1));
                var indices = Enumerable.Range(0, SymbolDefs.Count)
                    .OrderBy(_ => _random.Next())
                    .Take(count)
                    .ToList();

                lock (_lock)
                {
                    foreach (var idx in indices)
                    {
                        var def = SymbolDefs[idx];
                        var tick = GenerateTickForSymbol(def);
                        OnTickGenerated?.Invoke(tick);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MT5] MockTickGenerator error: {ex.Message}");
            }
        }

        private TickData GenerateTickForSymbol(SymbolDef def)
        {
            double prevBid = _currentBid[def.Symbol];
            double baseBid = def.BaseBid;

            // Calculate pip size based on digits
            double pipSize = def.Digits >= 3 && def.Category.StartsWith("Forex")
                ? Math.Pow(10, -(def.Digits - 1))  // For 5-digit forex, pip = 0.0001; for 3-digit JPY, pip = 0.01
                : Math.Pow(10, -def.Digits);

            // Volatility scaling per category
            double volatilityMultiplier = def.Category switch
            {
                "Forex Majors" => 0.8,
                "Forex Crosses" => 1.2,
                "Metals" => 2.5,
                "Indices" => 3.0,
                "Crypto" => 5.0,
                _ => 1.0
            };

            // Random walk component: normally distributed with slight mean reversion
            double randomComponent = (NextGaussian() * pipSize * volatilityMultiplier);

            // Mean reversion: pull price back toward base price (strength 0.002 per tick)
            double meanReversionStrength = 0.002;
            double deviation = prevBid - baseBid;
            double meanReversion = -deviation * meanReversionStrength;

            double newBid = prevBid + randomComponent + meanReversion;

            // Ensure price never goes negative or unrealistically far from base
            double maxDeviation = baseBid * 0.02; // Max 2% deviation from base
            newBid = Math.Max(baseBid - maxDeviation, Math.Min(baseBid + maxDeviation, newBid));
            newBid = Math.Max(pipSize, newBid); // Never go below one pip

            // Round to correct digits
            newBid = Math.Round(newBid, def.Digits);

            // Calculate spread in price terms
            double spreadInPrice = def.SpreadPips * pipSize;
            double ask = Math.Round(newBid + spreadInPrice, def.Digits);

            // Add a small random spread variation (0-30% of base spread)
            double spreadVariation = spreadInPrice * _random.NextDouble() * 0.3;
            ask = Math.Round(ask + spreadVariation, def.Digits);

            double spread = Math.Round(ask - newBid, def.Digits);

            // Update session high/low
            if (newBid > _sessionHigh[def.Symbol])
                _sessionHigh[def.Symbol] = newBid;
            if (newBid < _sessionLow[def.Symbol])
                _sessionLow[def.Symbol] = newBid;

            // Determine direction
            double previousBid = _previousBid[def.Symbol];
            string direction = newBid > previousBid ? "up" : newBid < previousBid ? "down" : "none";

            // Mock volume (random, category-scaled)
            double volume = def.Category switch
            {
                "Forex Majors" => _random.Next(50, 500) * 1000.0,
                "Forex Crosses" => _random.Next(20, 200) * 1000.0,
                "Metals" => _random.Next(10, 150) * 100.0,
                "Indices" => _random.Next(5, 100) * 10.0,
                "Crypto" => _random.Next(1, 50) * 0.1,
                _ => _random.Next(10, 100) * 100.0
            };

            // Store state
            _previousBid[def.Symbol] = _currentBid[def.Symbol];
            _currentBid[def.Symbol] = newBid;

            return new TickData
            {
                Symbol = def.Symbol,
                Bid = newBid,
                Ask = ask,
                Spread = spread,
                High = Math.Round(_sessionHigh[def.Symbol], def.Digits),
                Low = Math.Round(_sessionLow[def.Symbol], def.Digits),
                Digits = def.Digits,
                Volume = volume,
                Direction = direction,
                TickTime = DateTime.UtcNow
            };
        }

        private double NextGaussian()
        {
            // Box-Muller transform for normal distribution
            double u1 = 1.0 - _random.NextDouble();
            double u2 = 1.0 - _random.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }

        // Internal symbol definition record
        private record SymbolDef(
            string Symbol,
            double BaseBid,
            double SpreadPips,
            int Digits,
            double ContractSize,
            string ProfitCurrency,
            string MarginCurrency,
            int CalcMode,
            string Category);
    }
}
