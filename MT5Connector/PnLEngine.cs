namespace MT5Connector
{
    public static class PnLEngine
    {
        public static double CalculatePositionProfit(PositionData position, double bid, double ask, SymbolInfo symbol)
        {
            throw new NotImplementedException();
        }

        public static double GetConversionRate(string fromCurrency, string toCurrency, Dictionary<string, TickData> ticks)
        {
            throw new NotImplementedException();
        }

        public static void RecalculateAllProfits(List<PositionData> positions, Dictionary<string, TickData> ticks, Dictionary<string, SymbolInfo> symbols)
        {
            throw new NotImplementedException();
        }
    }
}
