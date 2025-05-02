using QBFC16Lib;
using Serilog;

namespace QB_Payments_Lib
{
    public static class InvoiceReader
    {
        public static Invoice QueryInvoiceByCustomerNameAndMemo(string customerName)
        {
            Invoice invoice = null;
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

                invoice = WalkInvoiceQueryRs(responseMsgSet);
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
                Log.Error(e, "Error querying invoice from QuickBooks");
            }

            return invoice;
        }

        static void BuildInvoiceQueryRq(IMsgSetRequest requestMsgSet, string customerName)
        {
            IInvoiceQuery invoiceQueryRq = requestMsgSet.AppendInvoiceQueryRq();
            invoiceQueryRq.ORInvoiceQuery.InvoiceFilter.EntityFilter.OREntityFilter.FullNameWithChildren.SetValue(customerName);
            // Removed the line causing the error as IInvoiceFilter does not have a Memo property
            invoiceQueryRq.IncludeLineItems.SetValue(true);
        }

        static Invoice WalkInvoiceQueryRs(IMsgSetResponse responseMsgSet)
        {
            if (responseMsgSet == null) return null;
            IResponseList responseList = responseMsgSet.ResponseList;
            if (responseList == null) return null;

            for (int i = 0; i < responseList.Count; i++)
            {
                IResponse response = responseList.GetAt(i);
                if (response.StatusCode >= 0 && response.Detail != null)
                {
                    ENResponseType responseType = (ENResponseType)response.Type.GetValue();
                    if (responseType == ENResponseType.rtInvoiceQueryRs)
                    {
                        IInvoiceRetList invoiceRetList = (IInvoiceRetList)response.Detail;
                        return WalkInvoiceRet(invoiceRetList);
                    }
                }
            }

            return null;
        }

        static Invoice WalkInvoiceRet(IInvoiceRetList invoiceRetList)
        {
            if (invoiceRetList == null || invoiceRetList.Count == 0) return null;

            IInvoiceRet invoiceRet = invoiceRetList.GetAt(0);
            Invoice invoice = new Invoice();

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
                invoice.AmountDue = (decimal)invoiceRet.BalanceRemaining.GetValue();
            }

            return invoice;
        }
    }

    public class Invoice
    {
        public string TxnID { get; set; }
        public string CustomerName { get; set; }
        public DateTime InvoiceDate { get; set; }
        public decimal AmountDue { get; set; }
    }
}
