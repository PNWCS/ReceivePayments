using QB_Payments_Lib;

namespace QB_Payments_ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            string customerName = "cvb1";


            // Step 1: Query the specific invoice by customer name and memo
            Invoice invoiceToPay = InvoiceReader.QueryInvoiceByCustomerNameAndMemo(customerName);



            if (invoiceToPay != null)
            {
                // Step 2: Create a payment for the selected invoice
                Payment payment = new Payment
                {
                    CustomerName = invoiceToPay.CustomerName,
                    PaymentDate = DateTime.Now,
                    CompanyID = 123, // Example CompanyID
                    InvoicesPaid = new List<string> { invoiceToPay.TxnID },
                    Amount = invoiceToPay.AmountDue // Pay the full amount due
                };

                // Step 3: Add the payment to QuickBooks
                PaymentAdder.AddPayments(new List<Payment> { payment });

                // Step 4: Display the details of the paid invoice
                Console.WriteLine("Paid Invoice:");
                Console.WriteLine($"Invoice ID: {invoiceToPay.TxnID}");
                Console.WriteLine($"Customer Name: {invoiceToPay.CustomerName}");
                Console.WriteLine($"Invoice Date: {invoiceToPay.InvoiceDate}");
                Console.WriteLine($"Amount Paid: {invoiceToPay.AmountDue}");
                Console.WriteLine("--------------------------------------------------");
            }
            else
            {
                Console.WriteLine($"No active invoice found for customer {customerName} with memo .");
            }
        }
    }
}
