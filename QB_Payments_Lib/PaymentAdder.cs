using QBFC16Lib;
using System;
using System.Collections.Generic;

namespace QB_Payments_Lib
{
    public class PaymentAdder
    {
        public static void AddPayments(List<Payment> payments)
        {
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

                // Build request for each payment
                foreach (var payment in payments)
                {
                    BuildReceivePaymentAddRq(requestMsgSet, payment);
                }

                // Connect to QuickBooks and begin a session
                sessionManager.OpenConnection("", "Sample Code from OSR");
                connectionOpen = true;
                sessionManager.BeginSession("", ENOpenMode.omDontCare);
                sessionBegun = true;

                // Send the request and get the response from QuickBooks
                IMsgSetResponse responseMsgSet = sessionManager.DoRequests(requestMsgSet);

                // Handle response and assign TxnIDs
                HandleAddPaymentResponse(responseMsgSet, payments);

                // End the session and close the connection to QuickBooks
                sessionManager.EndSession();
                sessionManager.CloseConnection();
            }
            catch (Exception e)
            {
                if (sessionBegun)
                {
                    sessionManager?.EndSession();
                }
                if (connectionOpen)
                {
                    sessionManager?.CloseConnection();
                }
                Console.WriteLine($"Error adding payments to QuickBooks: {e.Message}");
            }
        }

        private static void BuildReceivePaymentAddRq(IMsgSetRequest requestMsgSet, Payment payment)
        {
            IReceivePaymentAdd ReceivePaymentAddRq = requestMsgSet.AppendReceivePaymentAddRq();
            ReceivePaymentAddRq.CustomerRef.FullName.SetValue(payment.CustomerName);
            ReceivePaymentAddRq.TxnDate.SetValue(payment.PaymentDate);
            ReceivePaymentAddRq.TotalAmount.SetValue((double)payment.Amount);

            // Apply payment to specified invoices
            foreach (var invoiceTxnID in payment.InvoicesPaid)
            {
                var appliedToTxnAdd = ReceivePaymentAddRq.ORApplyPayment.AppliedToTxnAddList.Append();
                appliedToTxnAdd.TxnID.SetValue(invoiceTxnID);
                appliedToTxnAdd.PaymentAmount.SetValue((double)payment.Amount); // Apply full payment amount
                Console.WriteLine($"Applying payment to invoice TxnID: {invoiceTxnID} with amount: {payment.Amount}");
            }
        }

        private static void HandleAddPaymentResponse(IMsgSetResponse responseMsgSet, List<Payment> payments)
        {
            if (responseMsgSet == null || responseMsgSet.ResponseList == null) return;

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
                        payments[i].TxnID = receivePaymentRet.TxnID.GetValue();
                        Console.WriteLine($"Payment added successfully with TxnID: {receivePaymentRet.TxnID.GetValue()}");
                    }
                }
                else
                {
                    Console.WriteLine($"Error adding payment: {response.StatusMessage}");
                }
            }
        }
    }
}
