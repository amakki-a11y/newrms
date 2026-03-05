namespace MT5Connector
{
    public class MT5Config
    {
        public string ServerAddress { get; set; } = "89.21.67.56";
        public int ServerPort { get; set; } = 443;
        public ulong ManagerLogin { get; set; } = 1067;
        public string ManagerPassword { get; set; } = "@d4cBjLc";
        public int WsPort { get; set; } = 8181;
        public int TickThrottleMs { get; set; } = 100;
    }
}
