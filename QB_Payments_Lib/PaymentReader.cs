using QBFC16Lib;

namespace QB_Payments_Lib
{
    public static class PaymentReader
    {
        public static List<Payment> QueryAllPayments()
        {
            var payments = new List<Payment>();
            using var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME);

            IMsgSetRequest request = qbSession.CreateRequestSet();
            var paymentQuery = request.AppendReceivePaymentQueryRq();
            paymentQuery.IncludeLineItems.SetValue(true);

            var response = qbSession.SendRequest(request);
            var responseList = response.ResponseList;

            if (responseList == null || responseList.Count == 0)
                return payments;

            for (int i = 0; i < responseList.Count; i++)
            {
                var responseItem = responseList.GetAt(i);
                if (responseItem.StatusCode >= 0 && responseItem.Detail is IReceivePaymentRetList paymentRetList)
                {
                    for (int j = 0; j < paymentRetList.Count; j++)
                    {
                        var paymentRet = paymentRetList.GetAt(j);
                        var payment = new Payment
                        {
                            TxnID = paymentRet.TxnID.GetValue(),
                            CustomerName = paymentRet.CustomerRef.FullName.GetValue(),
                            PaymentDate = paymentRet.TxnDate.GetValue(),
                            Amount = (decimal)paymentRet.TotalAmount.GetValue(),
                            InvoicesPaid = paymentRet.AppliedToTxnRetList != null
                                ? Enumerable.Range(0, paymentRet.AppliedToTxnRetList.Count)
                                    .Select(index => paymentRet.AppliedToTxnRetList.GetAt(index).TxnID.GetValue())
                                    .ToList()
                                : new List<string>()
                        };

                        // Extract CompanyID from Memo  
                        if (paymentRet.Memo != null)
                        {
                            if (int.TryParse(paymentRet.Memo.GetValue(), out int companyID))
                            {
                                payment.CompanyID = companyID;
                            }
                            else
                            {
                                payment.CompanyID = 0; // Default to 0 if parsing fails  
                            }
                        }

                        payments.Add(payment);
                    }
                }
            }

            return payments;
        }
    }
}
