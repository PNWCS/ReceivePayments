using QB_Payments_Lib;

class Program
{
    static void Main(string[] args)
    {
        // Step 1: Define hardcoded payment entries
        var hardcodedPayments = new List<Payment>
        {
            new Payment
            {
                CustomerName = "cvf",
                PaymentDate = DateTime.Now,
                Amount = 670.00m,
                InvoicesPaid = new List<string>()
            },
            new Payment
            {
                CustomerName = "aaaa",
                PaymentDate = DateTime.Now,
                Amount = 502.00m,
                InvoicesPaid = new List<string>()
            },
        };

        // Step 2: Synchronize payments
        PaymentComparator.SyncPayments(hardcodedPayments);
    }
}
