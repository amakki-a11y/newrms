namespace MT5Connector
{
    public class PnLService
    {
        private readonly MT5Service _mt5;
        private readonly DbService _db;
        private Timer? _timer;

        public PnLService(MT5Service mt5, DbService db)
        {
            _mt5 = mt5;
            _db = db;
        }

        public void Start()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}
