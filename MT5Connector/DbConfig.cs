namespace MT5Connector
{
    public class DbConfig
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 5432;
        public string Database { get; set; } = "riskmanager";
        public string Username { get; set; } = "managers";
        public string Password { get; set; } = "managers_dev_password";
        public int MinPoolSize { get; set; } = 2;
        public int MaxPoolSize { get; set; } = 20;

        public string ConnectionString =>
            $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};Pooling=true;Minimum Pool Size={MinPoolSize};Maximum Pool Size={MaxPoolSize}";
    }
}
