using QB_Payments_Lib; // Reference the library for Customer, CustomerAdder, and InvoiceAdder
using QBFC16Lib;
using Serilog;
using static QB_Payments_Test.CommonMethods;   // EnsureLogFileClosed, ResetLogger, etc.

namespace QB_Payments_Test
{
    [Collection("Sequential Tests")]
    public class PaymentComparatorTests
    {
        private const int START_COMPANY_ID = 5000;      // Avoid collisions with real data

        public PaymentComparatorTests()
        {
            // Configure Serilog logger
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "logs", "test-log-.txt"), rollingInterval: RollingInterval.Day)
                .CreateLogger();
        }

        [Fact]
        public void ComparePayments_InMemoryScenario_And_Verify_Logs()
        {
            /* ---------- 0. Prepare logger ---------- */
            EnsureLogFileClosed();
            DeleteOldLogFiles();
            ResetLogger();

            Log.Information("Test started: ComparePayments_InMemoryScenario_And_Verify_Logs");

            /* ---------- 1. Create customers & invoices in QB ---------- */
            var random = new Random();
            var newCustomers = new List<Customer>();
            var newInvoices = new List<Invoice>();
            var initialPayments = new List<Payment>();

            for (int i = 0; i < 3; i++)
            {
                string custName = "PayTestCust_" + Guid.NewGuid().ToString("N")[..8];
                int cid = START_COMPANY_ID + i;

                newCustomers.Add(new Customer(custName, $"ACME_{cid}") { QB_ID = string.Empty });

                decimal amount = random.Next(100, 500);
                var inv = new Invoice
                {
                    CustomerName = custName,
                    InvoiceDate = DateTime.Today,
                    InvoiceNumber = "INV_" + Guid.NewGuid().ToString("N")[..6],
                    InoviceAmount = (double)amount, // Corrected property name
                    BalanceRemaining = amount,   // will be paid in full
                    CompanyID = cid
                };
                newInvoices.Add(inv);
            }

            // Add customers & invoices so ReceivePayment can reference them
            CustomerAdder.AddCustomers(newCustomers);
            InvoiceAdder.AddInvoices(newInvoices);

            // Refresh invoices to get TxnIDs written back
            var qbInvoices = new Dictionary<string, Invoice>();
            foreach (var customer in newCustomers)
            {
                var customerInvoices = InvoiceReader.QueryAllInvoices(customer.Name);
                foreach (var invoice in customerInvoices
                    .Where(inv => newInvoices.Any(x => x.InvoiceNumber == inv.InvoiceNumber)))
                {
                    if (invoice.InvoiceNumber != null) // Ensure InvoiceNumber is not null
                    {
                        qbInvoices[invoice.InvoiceNumber] = invoice;
                    }
                }
            }

            /* ---------- 2. Build initial in-memory payments list ---------- */
            foreach (var orig in newInvoices)
            {
                if (qbInvoices.TryGetValue(orig.InvoiceNumber, out var qbInv))
                {
                    initialPayments.Add(new Payment
                    {
                        CompanyID = orig.CompanyID,
                        CustomerName = orig.CustomerName,
                        PaymentDate = DateTime.Today,
                        Amount = (decimal)qbInv.InoviceAmount, // Corrected property name
                        InvoicesPaid = new() { qbInv.TxnID! }
                    });
                }
                else
                {
                    Log.Warning($"Invoice with InvoiceNumber '{orig.InvoiceNumber}' not found in QuickBooks.");
                }
            }

            List<Payment> firstCompareResult = new();
            List<Payment> updatedPayments = new(); // Declare updatedPayments here to fix the issue

            try
            {
                /* ---------- 3. FIRST compare – expect all Added ---------- */
                PaymentComparator.ComparePayments(initialPayments);

                foreach (var p in initialPayments.Where(p => initialPayments.Any(x => x.CompanyID == p.CompanyID)))
                    Assert.Equal(PaymentTermStatus.Added, p.Status);

                /* ---------- 4. Mutate list to force other statuses ---------- */
                updatedPayments = new List<Payment>(initialPayments); // Initialize updatedPayments

                if (updatedPayments.Count >= 2)
                {
                    var toRemove = updatedPayments[0];            // -> Missing
                    var toModify = updatedPayments[1];            // -> Different
                    updatedPayments.Remove(toRemove);
                    toModify.Amount += 1m;                          // change amount
                }
                else
                {
                    Log.Warning("Not enough payments in the list to perform mutation for testing.");
                }

                /* ---------- 5. SECOND compare ---------- */
                PaymentComparator.ComparePayments(updatedPayments);

                if (updatedPayments.Count >= 2)
                {
                    var toRemove = updatedPayments[0];
                    var toModify = updatedPayments[1];

                    Assert.Equal(PaymentTermStatus.Missing, toRemove.Status);
                    Assert.Equal(PaymentTermStatus.Different, toModify.Status);

                    var unchanged = updatedPayments
                                    .Where(p => p.CompanyID != toModify.CompanyID)
                                    .Select(p => p.CompanyID);
                    foreach (var id in unchanged)
                        Assert.Equal(PaymentTermStatus.Unchanged, updatedPayments.First(p => p.CompanyID == id).Status);
                }
            }
            finally
            {
                /* ---------- 6. Clean-up QB data ---------- */
                using var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME);

                // Payments
                foreach (var pay in initialPayments.Where(p => !string.IsNullOrEmpty(p.TxnID)))
                    DeleteReceivePayment(qbSession, pay.TxnID);

                // Invoices
                foreach (var inv in qbInvoices.Values)
                    DeleteInvoice(qbSession, inv.TxnID!);

                // Customers
                foreach (var cust in newCustomers.Where(c => !string.IsNullOrEmpty(c.QB_ID)))
                    DeleteCustomer(qbSession, cust.QB_ID);
            }

            /* ---------- 7. Log assertions ---------- */
            EnsureLogFileClosed();
            string logFile = GetLatestLogFile();
            EnsureLogFileExists(logFile);

            string logs = File.ReadAllText(logFile);
            Assert.Contains("PaymentsComparator Initialized", logs);
            Assert.Contains("PaymentsComparator Completed", logs);

            foreach (var p in initialPayments.Concat(updatedPayments)) // updatedPayments is now in scope
                Assert.Contains($"Payment {p.CompanyID} is {p.Status}.", logs);

            Log.Information("Test completed: ComparePayments_InMemoryScenario_And_Verify_Logs");
        }

        /* ===== Helper delete routines ===== */
        private static void DeleteReceivePayment(QuickBooksSession sess, string txnID)
        {
            IMsgSetRequest rq = sess.CreateRequestSet();
            ITxnDel delRq = rq.AppendTxnDelRq();
            delRq.TxnDelType.SetValue(ENTxnDelType.tdtReceivePayment);
            delRq.TxnID.SetValue(txnID);
            sess.SendRequest(rq);
        }

        private static void DeleteInvoice(QuickBooksSession sess, string txnID)
        {
            IMsgSetRequest rq = sess.CreateRequestSet();
            ITxnDel delRq = rq.AppendTxnDelRq();
            delRq.TxnDelType.SetValue(ENTxnDelType.tdtInvoice);
            delRq.TxnID.SetValue(txnID);
            sess.SendRequest(rq);
        }

        private static void DeleteCustomer(QuickBooksSession sess, string listID)
        {
            IMsgSetRequest rq = sess.CreateRequestSet();
            IListDel delRq = rq.AppendListDelRq();
            delRq.ListDelType.SetValue(ENListDelType.ldtCustomer);
            delRq.ListID.SetValue(listID);
            sess.SendRequest(rq);
        }
    }
}

