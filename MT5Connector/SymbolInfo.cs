namespace MT5Connector
{
    public class SymbolInfo
    {
        public string Symbol { get; set; } = "";
        public int Digits { get; set; }
        public double ContractSize { get; set; }
        public string ProfitCurrency { get; set; } = "";
        public string MarginCurrency { get; set; } = "";
        public int CalcMode { get; set; }
        public string Category { get; set; } = "";
    }
}
