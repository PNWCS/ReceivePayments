using QBFC16Lib;

namespace QB_Payments_Lib
{
    public class PaymentAdder
    {
        public static void AddPayments(List<Payment> payments)
        {
            using var sessionManager = new QuickBooksSession(AppConfig.QB_APP_NAME);

            try
            {
                // Create the message set request object to hold our request
                IMsgSetRequest requestMsgSet = sessionManager.CreateRequestSet();
                requestMsgSet.Attributes.OnError = ENRqOnError.roeContinue;

                // Build request for each payment
                foreach (var payment in payments)
                {
                    BuildReceivePaymentAddRq(sessionManager, requestMsgSet, payment);
                }

                // Send the request and get the response from QuickBooks
                IMsgSetResponse responseMsgSet = sessionManager.SendRequest(requestMsgSet);

                // Handle response and assign TxnIDs
                HandleAddPaymentResponse(responseMsgSet, payments);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error adding payments to QuickBooks: {e.Message}");
                throw; // Re-throw the exception to ensure the caller is aware of the failure
            }
        }

        private static void BuildReceivePaymentAddRq(QuickBooksSession sessionManager, IMsgSetRequest requestMsgSet, Payment payment)
        {
            IReceivePaymentAdd ReceivePaymentAddRq = requestMsgSet.AppendReceivePaymentAddRq();

            // Set required fields
            if (string.IsNullOrEmpty(payment.CustomerName))
            {
                throw new ArgumentException("CustomerName is required for adding a payment.");
            }

            ReceivePaymentAddRq.CustomerRef.FullName.SetValue(payment.CustomerName);
            ReceivePaymentAddRq.TxnDate.SetValue(payment.PaymentDate);
            ReceivePaymentAddRq.TotalAmount.SetValue((double)payment.Amount);

            // Apply payment to specified invoices
            double remainingPayment = (double)payment.Amount;

            foreach (var invoiceTxnID in payment.InvoicesPaid)
            {
                // Query the balance due for the invoice
                double balanceDue = GetInvoiceBalanceDue(sessionManager, invoiceTxnID);

                if (balanceDue <= 0)
                {
                    Console.WriteLine($"Skipping invoice TxnID: {invoiceTxnID} as it has no balance due.");
                    continue;
                }

                // Determine the payment amount for this invoice
                double paymentAmount = Math.Min(remainingPayment, balanceDue);

                var appliedToTxnAdd = ReceivePaymentAddRq.ORApplyPayment.AppliedToTxnAddList.Append();
                appliedToTxnAdd.TxnID.SetValue(invoiceTxnID);
                appliedToTxnAdd.PaymentAmount.SetValue(paymentAmount);

                Console.WriteLine($"Applying payment of {paymentAmount} to invoice TxnID: {invoiceTxnID} (Balance Due: {balanceDue})");

                // Reduce the remaining payment amount
                remainingPayment -= paymentAmount;

                // Stop if the payment amount has been fully allocated
                if (remainingPayment <= 0)
                {
                    break;
                }
            }
        }

        private static double GetInvoiceBalanceDue(QuickBooksSession sessionManager, string invoiceTxnID)
        {
            IMsgSetRequest request = sessionManager.CreateRequestSet();
            var invoiceQuery = request.AppendInvoiceQueryRq();
            invoiceQuery.ORInvoiceQuery.TxnIDList.Add(invoiceTxnID);

            var response = sessionManager.SendRequest(request);
            var responseList = response.ResponseList;

            if (responseList == null || responseList.Count == 0)
            {
                throw new Exception($"No response received for InvoiceQuery with TxnID: {invoiceTxnID}");
            }

            var responseItem = responseList.GetAt(0);
            if (responseItem.StatusCode != 0)
            {
                throw new Exception($"InvoiceQuery failed for TxnID: {invoiceTxnID}. Error: {responseItem.StatusMessage}");
            }

            var invoiceRetList = responseItem.Detail as IInvoiceRetList;
            if (invoiceRetList == null || invoiceRetList.Count == 0)
            {
                throw new Exception($"No invoice found for TxnID: {invoiceTxnID}");
            }

            var invoiceRet = invoiceRetList.GetAt(0);
            return invoiceRet.BalanceRemaining.GetValue();
        }

        private static void HandleAddPaymentResponse(IMsgSetResponse responseMsgSet, List<Payment> payments)
        {
            if (responseMsgSet == null || responseMsgSet.ResponseList == null)
            {
                throw new Exception("No response received from QuickBooks.");
            }

            IResponseList responseList = responseMsgSet.ResponseList;

            for (int i = 0; i < responseList.Count; i++)
            {
                IResponse response = responseList.GetAt(i);
                Console.WriteLine($"Response Status Code: {response.StatusCode}");
                Console.WriteLine($"Response Status Message: {response.StatusMessage}");

                if (response.StatusCode >= 0 && response.Detail != null)
                {
                    ENResponseType responseType = (ENResponseType)response.Type.GetValue();
                    if (responseType == ENResponseType.rtReceivePaymentAddRs)
                    {
                        IReceivePaymentRet receivePaymentRet = (IReceivePaymentRet)response.Detail;
                        string txnID = receivePaymentRet.TxnID.GetValue();

                        if (string.IsNullOrEmpty(txnID))
                        {
                            throw new Exception($"Payment for CompanyID={payments[i].CompanyID} did not get a TxnID.");
                        }

                        payments[i].TxnID = txnID;
                        Console.WriteLine($"Payment added successfully with TxnID: {txnID}");
                    }
                }
                else
                {
                    Console.WriteLine($"Error adding payment: {response.StatusMessage}");
                    throw new Exception($"Error adding payment for CompanyID={payments[i].CompanyID}: {response.StatusMessage}");
                }
            }
        }
    }
}

