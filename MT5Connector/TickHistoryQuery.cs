namespace MT5Connector
{
    public class OhlcCandle
    {
        public DateTime Time { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }
    }

    public class TickHistoryQuery
    {
        private readonly DbConfig _dbConfig;

        public TickHistoryQuery(DbConfig dbConfig)
        {
            _dbConfig = dbConfig;
        }

        public async Task<List<OhlcCandle>> GetCandles(string symbol, string interval, DateTime from, DateTime to, int limit = 500)
        {
            throw new NotImplementedException();
        }
    }
}
