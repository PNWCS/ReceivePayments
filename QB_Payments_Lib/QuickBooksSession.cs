using QBFC16Lib;

namespace QB_Payments_Lib
{
    public class QuickBooksSession : IDisposable
    {
        private QBSessionManager _sessionManager;

        public QuickBooksSession(string appName)
        {
            _sessionManager = new QBSessionManager();
            _sessionManager.OpenConnection("", appName);
            _sessionManager.BeginSession("", ENOpenMode.omDontCare);
        }

        public IMsgSetRequest CreateRequestSet()
        {
            return _sessionManager.CreateMsgSetRequest("US", 16, 0);
        }

        public IMsgSetResponse SendRequest(IMsgSetRequest requestMsgSet)
        {
            return _sessionManager.DoRequests(requestMsgSet);
        }

        public void Dispose()
        {
            _sessionManager.EndSession();
            _sessionManager.CloseConnection();
        }
    }
}
