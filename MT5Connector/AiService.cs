namespace MT5Connector
{
    public class AiService
    {
        private readonly AccountService _accountService;
        private readonly DbService _db;
        private readonly MT5Service _mt5;

        public AiService(AccountService accountService, DbService db, MT5Service mt5)
        {
            _accountService = accountService;
            _db = db;
            _mt5 = mt5;
        }

        public async Task ProcessChatMessage(string message, Action<string> onChunk, Action<string, object?> onAction, Action onDone)
        {
            throw new NotImplementedException();
        }
    }
}
