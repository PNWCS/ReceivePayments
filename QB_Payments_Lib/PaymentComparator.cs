namespace QB_Payments_Lib
{
    public class PaymentComparator
    {
        public static void SyncPayments(List<Payment> hardcodedPayments)
        {
            // Step 1: Fetch all payments from QuickBooks
            var quickBooksPayments = PaymentReader.QueryAllPayments();

            // Step 2: Compare payments and assign statuses
            foreach (var hardcodedPayment in hardcodedPayments)
            {
                var matchingPayment = quickBooksPayments.Find(qb =>
                    qb.CustomerName == hardcodedPayment.CustomerName &&
                    qb.PaymentDate.Date == hardcodedPayment.PaymentDate.Date);

                if (matchingPayment == null)
                {
                    // Payment is missing in QuickBooks
                    hardcodedPayment.Status = PaymentTermStatus.Missing;
                    PayCustomerInvoices(hardcodedPayment);
                    Console.WriteLine($"Marked as Missing: Customer: {hardcodedPayment.CustomerName}, Amount: {hardcodedPayment.Amount}");
                }
                else if (matchingPayment.Amount != hardcodedPayment.Amount)
                {
                    // Payment exists but the amount is different
                    hardcodedPayment.Status = PaymentTermStatus.Different;
                    Console.WriteLine($"Marked as Different: Customer: {hardcodedPayment.CustomerName}, Amount: {hardcodedPayment.Amount}");
                }
                else
                {
                    // Payment exists and is unchanged
                    hardcodedPayment.Status = PaymentTermStatus.Unchanged;
                    Console.WriteLine($"Marked as Unchanged: Customer: {hardcodedPayment.CustomerName}, Amount: {hardcodedPayment.Amount}");

                    // Step 3: Find invoices for the customer and pay them

                }
            }

            // Step 4: Identify payments in QuickBooks but not in the hardcoded list
            foreach (var quickBooksPayment in quickBooksPayments)
            {
                var existsInHardcoded = hardcodedPayments.Exists(hc =>
                    hc.CustomerName == quickBooksPayment.CustomerName &&
                    hc.PaymentDate.Date == quickBooksPayment.PaymentDate.Date &&
                    hc.Amount == quickBooksPayment.Amount);

                if (!existsInHardcoded)
                {
                    quickBooksPayment.Status = PaymentTermStatus.Missing;
                    Console.WriteLine($"Missing in Excel: Customer: {quickBooksPayment.CustomerName}, Amount: {quickBooksPayment.Amount}");
                }
            }

            // Step 5: Log the results
            Console.WriteLine("\nPayment Status Summary:");
            foreach (var payment in hardcodedPayments)
            {
                Console.WriteLine($"Customer: {payment.CustomerName}, Amount: {payment.Amount}, Status: {payment.Status}");
            }
        }

        private static void PayCustomerInvoices(Payment payment)
        {
            try
            {
                // Step 1: Find invoices for the customer using InvoiceReader
                var invoice = InvoiceReader.QueryInvoiceByCustomerNameAndMemo(payment.CustomerName);

                if (invoice == null)
                {
                    Console.WriteLine($"No invoices found for Customer: {payment.CustomerName}");
                    return;
                }

                // Step 2: Create a payment for the invoice
                payment.InvoicesPaid.Add(invoice.TxnID);
                payment.Amount = invoice.AmountDue;

                // Step 3: Use PaymentAdder to process the payment
                Console.WriteLine($"Processing payment for Invoice TxnID: {invoice.TxnID}, Customer: {invoice.CustomerName}, Amount: {invoice.AmountDue}");
                PaymentAdder.AddPayments(new List<Payment> { payment });

                Console.WriteLine($"Payment successfully processed for Invoice TxnID: {invoice.TxnID}, Customer: {invoice.CustomerName}");
            }
            catch (Exception ex)
            {
                payment.Status = PaymentTermStatus.FailedToAdd;
                Console.WriteLine($"Failed to process payment for Customer: {payment.CustomerName}, Error: {ex.Message}");
            }
        }
    }
}
