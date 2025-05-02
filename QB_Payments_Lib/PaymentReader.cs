using QBFC16Lib;
using Serilog;

namespace QB_Payments_Lib
{


    public class PaymentReader
    {
        public static List<Payment> QueryAllPayments()
        {
            List<Payment> payments = new List<Payment>();
            bool sessionBegun = false;
            bool connectionOpen = false;
            QBSessionManager sessionManager = null;

            try
            {
                // Create the session Manager object
                sessionManager = new QBSessionManager();

                // Create the message set request object to hold our request
                IMsgSetRequest requestMsgSet = sessionManager.CreateMsgSetRequest("US", 16, 0);
                requestMsgSet.Attributes.OnError = ENRqOnError.roeContinue;

                BuildReceivePaymentQueryRq(requestMsgSet);

                // Connect to QuickBooks and begin a session
                sessionManager.OpenConnection("", "Sample Code from OSR");
                connectionOpen = true;
                sessionManager.BeginSession("", ENOpenMode.omDontCare);
                sessionBegun = true;

                // Send the request and get the response from QuickBooks
                IMsgSetResponse responseMsgSet = sessionManager.DoRequests(requestMsgSet);

                // End the session and close the connection to QuickBooks
                sessionManager.EndSession();
                sessionBegun = false;
                sessionManager.CloseConnection();
                connectionOpen = false;

                payments = WalkReceivePaymentQueryRs(responseMsgSet);
            }
            catch (Exception e)
            {
                if (sessionBegun)
                {
                    sessionManager.EndSession();
                }
                if (connectionOpen)
                {
                    sessionManager.CloseConnection();
                }
                Log.Error(e, "Error querying payments from QuickBooks");
            }

            return payments;
        }



        static void BuildReceivePaymentQueryRq(IMsgSetRequest requestMsgSet)
        {
            IReceivePaymentQuery ReceivePaymentQueryRq = requestMsgSet.AppendReceivePaymentQueryRq();
            ReceivePaymentQueryRq.IncludeLineItems.SetValue(true);
            // Add any necessary filters here
        }

        static List<Payment> WalkReceivePaymentQueryRs(IMsgSetResponse responseMsgSet)
        {
            List<Payment> payments = new List<Payment>();

            if (responseMsgSet == null) return payments;
            IResponseList responseList = responseMsgSet.ResponseList;
            if (responseList == null) return payments;

            for (int i = 0; i < responseList.Count; i++)
            {
                IResponse response = responseList.GetAt(i);
                if (response.StatusCode >= 0 && response.Detail != null)
                {
                    ENResponseType responseType = (ENResponseType)response.Type.GetValue();
                    if (responseType == ENResponseType.rtReceivePaymentQueryRs)
                    {
                        IReceivePaymentRetList ReceivePaymentRet = (IReceivePaymentRetList)response.Detail;
                        payments.AddRange(WalkReceivePaymentRet(ReceivePaymentRet));
                    }
                }
            }

            return payments;
        }

        static List<Payment> WalkReceivePaymentRet(IReceivePaymentRetList ReceivePaymentRet)
        {
            List<Payment> payments = new List<Payment>();

            if (ReceivePaymentRet == null) return payments;

            for (int i = 0; i < ReceivePaymentRet.Count; i++)
            {
                IReceivePaymentRet paymentRet = ReceivePaymentRet.GetAt(i);
                Payment payment = new Payment();

                if (paymentRet.CustomerRef != null && paymentRet.CustomerRef.FullName != null)
                {
                    payment.CustomerName = paymentRet.CustomerRef.FullName.GetValue();
                }

                if (paymentRet.TxnDate != null)
                {
                    payment.PaymentDate = paymentRet.TxnDate.GetValue();
                }

                if (paymentRet.TxnID != null)
                {
                    payment.TxnID = paymentRet.TxnID.GetValue();
                }

                if (paymentRet.Memo != null)
                {
                    if (int.TryParse(paymentRet.Memo.GetValue(), out int companyId))
                    {
                        payment.CompanyID = companyId;
                    }
                    else
                    {
                        Log.Warning("Failed to convert Memo to CompanyID for payment with TxnID: {TxnID}", payment.TxnID);
                    }
                }

                //if (paymentRet.AppliedToTxnRetList == null)
                //{
                //    Console.WriteLine("AppliedToTxnRetList is null");
                //}
                //else if (paymentRet.AppliedToTxnRetList.Count == 0)
                //{
                //    Console.WriteLine("AppliedToTxnRetList is empty");
                //}

                // Extract InvoiceTxnIDs
                if (paymentRet.AppliedToTxnRetList != null)
                {
                    for (int j = 0; j < paymentRet.AppliedToTxnRetList.Count; j++)
                    {
                        IAppliedToTxnRet appliedToTxnRet = paymentRet.AppliedToTxnRetList.GetAt(j);
                        if (appliedToTxnRet.TxnID != null)
                        {
                            payment.InvoicesPaid.Add(appliedToTxnRet.TxnID.GetValue());
                        }
                    }
                }

                payments.Add(payment);
            }

            return payments;
        }
    }

    /// <summary>
    /// Represents a session with QuickBooks.
    /// </summary>
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

        public IMsgSetResponse SendRequest(IMsgSetRequest request)
        {
            return _sessionManager.DoRequests(request);
        }

        public void Dispose()
        {
            if (_sessionManager != null)
            {
                _sessionManager.EndSession();
                _sessionManager.CloseConnection();
                _sessionManager = null;
            }
        }
    }
}
