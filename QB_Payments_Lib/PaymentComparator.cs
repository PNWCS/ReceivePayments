using Serilog;

namespace QB_Payments_Lib
{
    public class PaymentComparator
    {
        public static void ComparePayments(List<Payment> hardcodedPayments)
        {
            Log.Information("PaymentsComparator Initialized");
            Console.WriteLine("PaymentsComparator Initialized");

            // Step 1: Fetch all payments from QuickBooks
            var quickBooksPayments = PaymentReader.QueryAllPayments();
            Console.WriteLine($"Fetched {quickBooksPayments.Count} payments from QuickBooks.");
            Log.Information($"Fetched {quickBooksPayments.Count} payments from QuickBooks.");

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
                    Console.WriteLine($"Payment Missing: Customer = {hardcodedPayment.CustomerName}, Amount = {hardcodedPayment.Amount}");
                    Log.Warning($"Payment Missing: Customer = {hardcodedPayment.CustomerName}, Amount = {hardcodedPayment.Amount}");
                }
                else if (matchingPayment.Amount != hardcodedPayment.Amount)
                {
                    // Payment exists but the amount is different
                    hardcodedPayment.Status = PaymentTermStatus.Different;
                    Console.WriteLine($"Payment Different: Customer = {hardcodedPayment.CustomerName}, Expected Amount = {hardcodedPayment.Amount}, Actual Amount = {matchingPayment.Amount}");
                    Log.Warning($"Payment Different: Customer = {hardcodedPayment.CustomerName}, Expected Amount = {hardcodedPayment.Amount}, Actual Amount = {matchingPayment.Amount}");
                }
                else
                {
                    // Payment exists and is unchanged
                    hardcodedPayment.Status = PaymentTermStatus.Unchanged;
                    Console.WriteLine($"Payment Unchanged: Customer = {hardcodedPayment.CustomerName}, Amount = {hardcodedPayment.Amount}");
                    Log.Information($"Payment Unchanged: Customer = {hardcodedPayment.CustomerName}, Amount = {hardcodedPayment.Amount}");
                }
            }

            // Step 3: Identify payments in QuickBooks but not in the hardcoded list
            foreach (var quickBooksPayment in quickBooksPayments)
            {
                var existsInHardcoded = hardcodedPayments.Exists(hc =>
                    hc.CustomerName == quickBooksPayment.CustomerName &&
                    hc.PaymentDate.Date == quickBooksPayment.PaymentDate.Date &&
                    hc.Amount == quickBooksPayment.Amount);

                if (!existsInHardcoded)
                {
                    quickBooksPayment.Status = PaymentTermStatus.Missing;
                    Console.WriteLine($"Payment Missing in Hardcoded List: Customer = {quickBooksPayment.CustomerName}, Amount = {quickBooksPayment.Amount}");
                    Log.Warning($"Payment Missing in Hardcoded List: Customer = {quickBooksPayment.CustomerName}, Amount = {quickBooksPayment.Amount}");
                }
            }

            // Step 4: Log the results
            Console.WriteLine("\nPayment Status Summary:");
            Log.Information("\nPayment Status Summary:");
            foreach (var payment in hardcodedPayments)
            {
                Console.WriteLine($"Customer: {payment.CustomerName}, Amount: {payment.Amount}, Status: {payment.Status}");
                Log.Information($"Customer: {payment.CustomerName}, Amount: {payment.Amount}, Status: {payment.Status}");
            }

            Log.Information("PaymentsComparator Completed");
            Console.WriteLine("PaymentsComparator Completed");
        }
    }
}
