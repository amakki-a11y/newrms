namespace MT5Connector
{
    public class AccountData
    {
        public long Login { get; set; }
        public string Name { get; set; } = "";
        public string Group { get; set; } = "";
        public double Balance { get; set; }
        public double Credit { get; set; }
        public int Leverage { get; set; }
        public double Equity { get; set; }
        public double Margin { get; set; }
        public double MarginFree { get; set; }
        public double MarginLevel { get; set; }
        public double Profit { get; set; }
        public double Floating { get; set; }
        public int OpenPositions { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class PositionData
    {
        public long Ticket { get; set; }
        public long Login { get; set; }
        public string Symbol { get; set; } = "";
        public int Action { get; set; } // 0=Buy, 1=Sell
        public long Volume { get; set; } // lots * 10000
        public double PriceOpen { get; set; }
        public double PriceCurrent { get; set; }
        public double PriceSl { get; set; }
        public double PriceTp { get; set; }
        public double Profit { get; set; }
        public double Storage { get; set; }
        public int Digits { get; set; }
        public DateTime TimeCreate { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class AccountSummary
    {
        public int TotalAccounts { get; set; }
        public double TotalBalance { get; set; }
        public double TotalEquity { get; set; }
        public double TotalMargin { get; set; }
        public double TotalMarginFree { get; set; }
        public double TotalProfit { get; set; }
    }

    public class ClosePositionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public long Ticket { get; set; }
    }

    public class OpenPositionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public long Ticket { get; set; }
    }

    public class ModifyPositionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public long Ticket { get; set; }
    }

    public class DealEventData
    {
        public long DealId { get; set; }
        public long Login { get; set; }
        public string Symbol { get; set; } = "";
        public int Action { get; set; }
        public long Volume { get; set; }
        public double Price { get; set; }
        public double Profit { get; set; }
        public string Comment { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class SymbolExposure
    {
        public string Symbol { get; set; } = "";
        public double LongVolume { get; set; }
        public double ShortVolume { get; set; }
        public double NetVolume { get; set; }
        public double LongProfit { get; set; }
        public double ShortProfit { get; set; }
        public double NetProfit { get; set; }
    }

    public class AccountMover
    {
        public long Login { get; set; }
        public string Name { get; set; } = "";
        public double Profit { get; set; }
        public double ProfitChange { get; set; }
    }

    public class DealHistoryBucket
    {
        public DateTime BucketTime { get; set; }
        public double TotalProfit { get; set; }
        public int DealCount { get; set; }
    }

    public class PaginatedAccountResult
    {
        public List<AccountData> Accounts { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public AccountSummary? Summary { get; set; }
    }
}
