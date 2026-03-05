using Newtonsoft.Json;

namespace MT5Connector
{
    public class TickData
    {
        public string Symbol { get; set; } = "";
        public double Bid { get; set; }
        public double Ask { get; set; }
        public double Spread { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public int Digits { get; set; }
        public double Volume { get; set; }
        public string Direction { get; set; } = ""; // "up", "down", "none"
        public DateTime TickTime { get; set; }
    }

    public class WsMessage
    {
        public string Type { get; set; } = "";
        public object? Data { get; set; }
        public string? RequestId { get; set; }
    }

    public class SnapshotMessage
    {
        public string Type { get; set; } = "snapshot";
        public List<TickData> Data { get; set; } = new();
    }
}
