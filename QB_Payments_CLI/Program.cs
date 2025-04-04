namespace QB_Payments_Lib
{
    class Program
    {
        static void Main(string[] args)
        {

            //var result = QBDataDeleter.DeleteAllData();
            //Console.WriteLine("\nDeletion completed successfully!");
            //Console.WriteLine($"Deleted {result.payments} payments");
            //Console.WriteLine($"Deleted {result.invoices} invoices");
            //Console.WriteLine($"Deleted {result.customers} customers");
            PaymentReader.QueryAllPayments();

        }
    }
}
