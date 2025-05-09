using QBFC16Lib;
using Serilog;

namespace QB_Payments_Lib
{
    public static class InvoiceReader
    {
        public static List<Invoice> QueryAllInvoices(string customerName)
        {
            var invoices = new List<Invoice>();
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

                BuildInvoiceQueryRq(requestMsgSet, customerName);

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

                invoices = WalkInvoiceQueryRs(responseMsgSet);
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
                Log.Error(e, "Error querying invoices from QuickBooks");
            }

            return invoices;
        }

        static void BuildInvoiceQueryRq(IMsgSetRequest requestMsgSet, string customerName)
        {
            IInvoiceQuery invoiceQueryRq = requestMsgSet.AppendInvoiceQueryRq();
            invoiceQueryRq.ORInvoiceQuery.InvoiceFilter.EntityFilter.OREntityFilter.FullNameWithChildren.SetValue(customerName);
            invoiceQueryRq.IncludeLineItems.SetValue(true);
        }

        static List<Invoice> WalkInvoiceQueryRs(IMsgSetResponse responseMsgSet)
        {
            var invoices = new List<Invoice>();

            if (responseMsgSet == null) return invoices;
            IResponseList responseList = responseMsgSet.ResponseList;
            if (responseList == null) return invoices;

            for (int i = 0; i < responseList.Count; i++)
            {
                IResponse response = responseList.GetAt(i);
                if (response.StatusCode >= 0 && response.Detail != null)
                {
                    ENResponseType responseType = (ENResponseType)response.Type.GetValue();
                    if (responseType == ENResponseType.rtInvoiceQueryRs)
                    {
                        IInvoiceRetList invoiceRetList = (IInvoiceRetList)response.Detail;
                        invoices.AddRange(WalkInvoiceRetList(invoiceRetList));
                    }
                }
            }

            return invoices;
        }

        static List<Invoice> WalkInvoiceRetList(IInvoiceRetList invoiceRetList)
        {
            var invoices = new List<Invoice>();

            if (invoiceRetList == null || invoiceRetList.Count == 0) return invoices;

            for (int i = 0; i < invoiceRetList.Count; i++)
            {
                IInvoiceRet invoiceRet = invoiceRetList.GetAt(i);
                var invoice = new Invoice();

                if (invoiceRet.CustomerRef != null && invoiceRet.CustomerRef.FullName != null)
                {
                    invoice.CustomerName = invoiceRet.CustomerRef.FullName.GetValue();
                }

                if (invoiceRet.TxnDate != null)
                {
                    invoice.InvoiceDate = invoiceRet.TxnDate.GetValue();
                }

                if (invoiceRet.TxnID != null)
                {
                    invoice.TxnID = invoiceRet.TxnID.GetValue();
                }

                if (invoiceRet.BalanceRemaining != null)
                {
                    invoice.InoviceAmount = invoiceRet.BalanceRemaining.GetValue();
                }

                if (invoiceRet.RefNumber != null)
                {
                    invoice.InvoiceNumber = invoiceRet.RefNumber.GetValue();
                }

                invoices.Add(invoice);
            }

            return invoices;
        }
    }

    public class Invoice
    {
        public string TxnID { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public double InoviceAmount { get; set; }

        // New properties
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal BalanceRemaining { get; set; }
        public int CompanyID { get; set; }
    }
}
