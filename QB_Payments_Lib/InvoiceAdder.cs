using QBFC16Lib;

namespace QB_Payments_Lib
{
    public static class InvoiceAdder
    {
        public static void AddInvoices(List<Invoice> invoices)
        {
            using var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME);
            foreach (var invoice in invoices)
            {
                IMsgSetRequest request = qbSession.CreateRequestSet();
                IInvoiceAdd invoiceAddRq = request.AppendInvoiceAddRq();
                invoiceAddRq.CustomerRef.FullName.SetValue(invoice.CustomerName);
                invoiceAddRq.TxnDate.SetValue(invoice.InvoiceDate);
                invoiceAddRq.ORInvoiceLineAddList.Append().InvoiceLineAdd.Amount.SetValue(invoice.InoviceAmount);

                IMsgSetResponse response = qbSession.SendRequest(request);
                IResponse qbResponse = response.ResponseList.GetAt(0);
                if (qbResponse.StatusCode == 0 && qbResponse.Detail is IInvoiceRet invoiceRet)
                {
                    invoice.TxnID = invoiceRet.TxnID.GetValue();
                }
            }
        }
    }
}
