using QBFC16Lib;
using Serilog;

namespace QB_Deletion_Lib
{
    public class QBDataDeleter
    {
        // Delete all payments from QuickBooks
        public static int DeleteAllPayments()
        {
            int deletedCount = 0;
            bool sessionBegun = false;
            bool connectionOpen = false;
            QBSessionManager sessionManager = null;

            try
            {
                // Create the session Manager object
                sessionManager = new QBSessionManager();

                // Connect to QuickBooks and begin a session
                sessionManager.OpenConnection("", "QB Data Cleanup Tool");
                connectionOpen = true;
                sessionManager.BeginSession("", ENOpenMode.omDontCare);
                sessionBegun = true;

                // First, get all payment TxnIDs
                List<string> paymentTxnIDs = GetAllPaymentTxnIDs(sessionManager);

                // Delete each payment
                foreach (string txnID in paymentTxnIDs)
                {
                    if (DeletePayment(sessionManager, txnID))
                    {
                        deletedCount++;
                        Console.WriteLine($"Deleted payment with TxnID: {txnID}");
                    }
                }

                // End the session and close the connection to QuickBooks
                sessionManager.EndSession();
                sessionBegun = false;
                sessionManager.CloseConnection();
                connectionOpen = false;
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
                Log.Error(e, "Error deleting payments from QuickBooks");
            }

            return deletedCount;
        }

        // Delete all invoices from QuickBooks
        public static int DeleteAllInvoices()
        {
            int deletedCount = 0;
            bool sessionBegun = false;
            bool connectionOpen = false;
            QBSessionManager sessionManager = null;

            try
            {
                // Create the session Manager object
                sessionManager = new QBSessionManager();

                // Connect to QuickBooks and begin a session
                sessionManager.OpenConnection("", "QB Data Cleanup Tool");
                connectionOpen = true;
                sessionManager.BeginSession("", ENOpenMode.omDontCare);
                sessionBegun = true;

                // First, get all invoice TxnIDs
                List<string> invoiceTxnIDs = GetAllInvoiceTxnIDs(sessionManager);

                // Delete each invoice
                foreach (string txnID in invoiceTxnIDs)
                {
                    if (DeleteInvoice(sessionManager, txnID))
                    {
                        deletedCount++;
                        Console.WriteLine($"Deleted invoice with TxnID: {txnID}");
                    }
                }

                // End the session and close the connection to QuickBooks
                sessionManager.EndSession();
                sessionBegun = false;
                sessionManager.CloseConnection();
                connectionOpen = false;
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
                Log.Error(e, "Error deleting invoices from QuickBooks");
            }

            return deletedCount;
        }

        // Delete all customers from QuickBooks
        public static int DeleteAllCustomers()
        {
            int deletedCount = 0;
            bool sessionBegun = false;
            bool connectionOpen = false;
            QBSessionManager sessionManager = null;

            try
            {
                // Create the session Manager object
                sessionManager = new QBSessionManager();

                // Connect to QuickBooks and begin a session
                sessionManager.OpenConnection("", "QB Data Cleanup Tool");
                connectionOpen = true;
                sessionManager.BeginSession("", ENOpenMode.omDontCare);
                sessionBegun = true;

                // First, get all customer ListIDs
                List<string> customerListIDs = GetAllCustomerListIDs(sessionManager);

                // Delete each customer
                foreach (string listID in customerListIDs)
                {
                    if (DeleteCustomer(sessionManager, listID))
                    {
                        deletedCount++;
                        Console.WriteLine($"Deleted customer with ListID: {listID}");
                    }
                }

                // End the session and close the connection to QuickBooks
                sessionManager.EndSession();
                sessionBegun = false;
                sessionManager.CloseConnection();
                connectionOpen = false;
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
                Log.Error(e, "Error deleting customers from QuickBooks");
            }

            return deletedCount;
        }

        // Get all payment TxnIDs
        private static List<string> GetAllPaymentTxnIDs(QBSessionManager sessionManager)
        {
            List<string> txnIDs = new List<string>();

            // Create the message set request object to hold our request
            IMsgSetRequest requestMsgSet = sessionManager.CreateMsgSetRequest("US", 16, 0);
            requestMsgSet.Attributes.OnError = ENRqOnError.roeContinue;

            // Build a payment query request
            IReceivePaymentQuery paymentQueryRq = requestMsgSet.AppendReceivePaymentQueryRq();

            // Send the request and get the response from QuickBooks
            IMsgSetResponse responseMsgSet = sessionManager.DoRequests(requestMsgSet);

            if (responseMsgSet != null && responseMsgSet.ResponseList != null)
            {
                for (int i = 0; i < responseMsgSet.ResponseList.Count; i++)
                {
                    IResponse response = responseMsgSet.ResponseList.GetAt(i);
                    if (response.StatusCode >= 0 && response.Detail != null)
                    {
                        ENResponseType responseType = (ENResponseType)response.Type.GetValue();
                        if (responseType == ENResponseType.rtReceivePaymentQueryRs)
                        {
                            IReceivePaymentRetList paymentRetList = (IReceivePaymentRetList)response.Detail;
                            for (int j = 0; j < paymentRetList.Count; j++)
                            {
                                IReceivePaymentRet paymentRet = paymentRetList.GetAt(j);
                                if (paymentRet.TxnID != null)
                                {
                                    txnIDs.Add(paymentRet.TxnID.GetValue());
                                }
                            }
                        }
                    }
                }
            }

            return txnIDs;
        }

        // Get all invoice TxnIDs
        private static List<string> GetAllInvoiceTxnIDs(QBSessionManager sessionManager)
        {
            List<string> txnIDs = new List<string>();

            // Create the message set request object to hold our request
            IMsgSetRequest requestMsgSet = sessionManager.CreateMsgSetRequest("US", 16, 0);
            requestMsgSet.Attributes.OnError = ENRqOnError.roeContinue;

            // Build an invoice query request
            IInvoiceQuery invoiceQueryRq = requestMsgSet.AppendInvoiceQueryRq();

            // Send the request and get the response from QuickBooks
            IMsgSetResponse responseMsgSet = sessionManager.DoRequests(requestMsgSet);

            if (responseMsgSet != null && responseMsgSet.ResponseList != null)
            {
                for (int i = 0; i < responseMsgSet.ResponseList.Count; i++)
                {
                    IResponse response = responseMsgSet.ResponseList.GetAt(i);
                    if (response.StatusCode >= 0 && response.Detail != null)
                    {
                        ENResponseType responseType = (ENResponseType)response.Type.GetValue();
                        if (responseType == ENResponseType.rtInvoiceQueryRs)
                        {
                            IInvoiceRetList invoiceRetList = (IInvoiceRetList)response.Detail;
                            for (int j = 0; j < invoiceRetList.Count; j++)
                            {
                                IInvoiceRet invoiceRet = invoiceRetList.GetAt(j);
                                if (invoiceRet.TxnID != null)
                                {
                                    txnIDs.Add(invoiceRet.TxnID.GetValue());
                                }
                            }
                        }
                    }
                }
            }

            return txnIDs;
        }

        // Get all customer ListIDs
        private static List<string> GetAllCustomerListIDs(QBSessionManager sessionManager)
        {
            List<string> listIDs = new List<string>();

            // Create the message set request object to hold our request
            IMsgSetRequest requestMsgSet = sessionManager.CreateMsgSetRequest("US", 16, 0);
            requestMsgSet.Attributes.OnError = ENRqOnError.roeContinue;

            // Build a customer query request
            ICustomerQuery customerQueryRq = requestMsgSet.AppendCustomerQueryRq();

            // Send the request and get the response from QuickBooks
            IMsgSetResponse responseMsgSet = sessionManager.DoRequests(requestMsgSet);

            if (responseMsgSet != null && responseMsgSet.ResponseList != null)
            {
                for (int i = 0; i < responseMsgSet.ResponseList.Count; i++)
                {
                    IResponse response = responseMsgSet.ResponseList.GetAt(i);
                    if (response.StatusCode >= 0 && response.Detail != null)
                    {
                        ENResponseType responseType = (ENResponseType)response.Type.GetValue();
                        if (responseType == ENResponseType.rtCustomerQueryRs)
                        {
                            ICustomerRetList customerRetList = (ICustomerRetList)response.Detail;
                            for (int j = 0; j < customerRetList.Count; j++)
                            {
                                ICustomerRet customerRet = customerRetList.GetAt(j);
                                if (customerRet.ListID != null)
                                {
                                    listIDs.Add(customerRet.ListID.GetValue());
                                }
                            }
                        }
                    }
                }
            }

            return listIDs;
        }

        // Delete a specific payment by TxnID
        private static bool DeletePayment(QBSessionManager sessionManager, string txnID)
        {
            // Create the message set request object to hold our request
            IMsgSetRequest requestMsgSet = sessionManager.CreateMsgSetRequest("US", 16, 0);
            requestMsgSet.Attributes.OnError = ENRqOnError.roeContinue;

            // Build a TxnDel request
            ITxnDel txnDelRq = requestMsgSet.AppendTxnDelRq();
            txnDelRq.TxnDelType.SetValue(ENTxnDelType.tdtReceivePayment);
            txnDelRq.TxnID.SetValue(txnID);

            // Send the request and get the response from QuickBooks
            IMsgSetResponse responseMsgSet = sessionManager.DoRequests(requestMsgSet);

            if (responseMsgSet != null && responseMsgSet.ResponseList != null)
            {
                if (responseMsgSet.ResponseList.Count > 0)
                {
                    IResponse response = responseMsgSet.ResponseList.GetAt(0);
                    return response.StatusCode >= 0;
                }
            }

            return false;
        }

        // Delete a specific invoice by TxnID
        private static bool DeleteInvoice(QBSessionManager sessionManager, string txnID)
        {
            // Create the message set request object to hold our request
            IMsgSetRequest requestMsgSet = sessionManager.CreateMsgSetRequest("US", 16, 0);
            requestMsgSet.Attributes.OnError = ENRqOnError.roeContinue;

            // Build a TxnDel request
            ITxnDel txnDelRq = requestMsgSet.AppendTxnDelRq();
            txnDelRq.TxnDelType.SetValue(ENTxnDelType.tdtInvoice);
            txnDelRq.TxnID.SetValue(txnID);

            // Send the request and get the response from QuickBooks
            IMsgSetResponse responseMsgSet = sessionManager.DoRequests(requestMsgSet);

            if (responseMsgSet != null && responseMsgSet.ResponseList != null)
            {
                if (responseMsgSet.ResponseList.Count > 0)
                {
                    IResponse response = responseMsgSet.ResponseList.GetAt(0);
                    return response.StatusCode >= 0;
                }
            }

            return false;
        }

        // Delete a specific customer by ListID
        private static bool DeleteCustomer(QBSessionManager sessionManager, string listID)
        {
            // Create the message set request object to hold our request
            IMsgSetRequest requestMsgSet = sessionManager.CreateMsgSetRequest("US", 16, 0);
            requestMsgSet.Attributes.OnError = ENRqOnError.roeContinue;

            // Build a ListDel request
            IListDel listDelRq = requestMsgSet.AppendListDelRq();
            listDelRq.ListDelType.SetValue(ENListDelType.ldtCustomer);
            listDelRq.ListID.SetValue(listID);

            // Send the request and get the response from QuickBooks
            IMsgSetResponse responseMsgSet = sessionManager.DoRequests(requestMsgSet);

            if (responseMsgSet != null && responseMsgSet.ResponseList != null)
            {
                if (responseMsgSet.ResponseList.Count > 0)
                {
                    IResponse response = responseMsgSet.ResponseList.GetAt(0);
                    return response.StatusCode >= 0;
                }
            }

            return false;
        }

        // Delete all data in the proper order
        public static (int payments, int invoices, int customers) DeleteAllData()
        {
            // Delete in proper order to handle dependencies
            int payments = DeleteAllPayments();
            int invoices = DeleteAllInvoices();
            int customers = DeleteAllCustomers();

            return (payments, invoices, customers);
        }
    }
}