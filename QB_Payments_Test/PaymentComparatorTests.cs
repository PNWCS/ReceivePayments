using System.Diagnostics;
using Serilog;
using QB_Payments_Lib;
using QB_Customers_Lib;
using QB_Invoices_Lib;
using QBFC16Lib;
using static QB_Payments_Test.CommonMethods;   // EnsureLogFileClosed, ResetLogger, etc.

namespace QB_Payments_Test
{
    [Collection("Sequential Tests")]
    public class PaymentComparatorTests
    {
        private const int START_COMPANY_ID = 5000;      // Avoid collisions with real data

        [Fact]
        public void ComparePayments_InMemoryScenario_And_Verify_Logs()
        {
            /* ---------- 0. Prepare logger ---------- */
            EnsureLogFileClosed();
            DeleteOldLogFiles();
            ResetLogger();

            /* ---------- 1. Create customers & invoices in QB ---------- */
            var random = new Random();
            var newCustomers   = new List<Customer>();
            var newInvoices    = new List<Invoice>();
            var initialPayments = new List<Payment>();

            for (int i = 0; i < 3; i++)
            {
                string custName = "PayTestCust_" + Guid.NewGuid().ToString("N")[..8];
                int    cid      = START_COMPANY_ID + i;

                newCustomers.Add(new Customer(custName, $"ACME_{cid}") { QB_ID = string.Empty });

                decimal amount  = random.Next(100, 500);
                var inv = new Invoice
                {
                    CustomerName   = custName,
                    InvoiceDate    = DateTime.Today,
                    InvoiceNumber  = "INV_" + Guid.NewGuid().ToString("N")[..6],
                    InoviceAmount  = amount,
                    BalanceRemaining = amount,   // will be paid in full
                    CompanyID      = cid
                };
                newInvoices.Add(inv);
            }

            // Add customers & invoices so ReceivePayment can reference them
            CustomerAdder.AddCustomers(newCustomers);
            InvoiceAdder.AddInvoices(newInvoices);

            // Refresh invoices to get TxnIDs written back
            var qbInvoices = InvoiceReader.QueryAllInvoices()
                                          .Where(inv => newInvoices.Any(x => x.InvoiceNumber == inv.InvoiceNumber))
                                          .ToDictionary(inv => inv.InvoiceNumber);

            /* ---------- 2. Build initial in-memory payments list ---------- */
            foreach (var orig in newInvoices)
            {
                var qbInv = qbInvoices[orig.InvoiceNumber];
                initialPayments.Add(new Payment
                {
                    CompanyID     = orig.CompanyID,
                    CustomerName  = orig.CustomerName,
                    PaymentDate   = DateTime.Today,
                    Amount        = qbInv.InoviceAmount!.Value,
                    InvoicesPaid  = new() { qbInv.TxnID! }
                });
            }

            List<Payment> firstCompareResult  = new();
            List<Payment> secondCompareResult = new();

            try
            {
                /* ---------- 3. FIRST compare – expect all Added ---------- */
                firstCompareResult = PaymentsComparator.ComparePayments(initialPayments);

                foreach (var p in firstCompareResult.Where(p => initialPayments.Any(x => x.CompanyID == p.CompanyID)))
                    Assert.Equal(PaymentStatus.Added, p.Status);

                /* ---------- 4. Mutate list to force other statuses ---------- */
                var updatedPayments = new List<Payment>(initialPayments);

                var toRemove   = updatedPayments[0];            // -> Missing
                var toModify   = updatedPayments[1];            // -> Different
                updatedPayments.Remove(toRemove);
                toModify.Amount += 1m;                          // change amount

                /* ---------- 5. SECOND compare ---------- */
                secondCompareResult = PaymentsComparator.ComparePayments(updatedPayments);
                var secondDict = secondCompareResult.ToDictionary(p => p.CompanyID);

                // Missing
                Assert.Equal(PaymentStatus.Missing, secondDict[toRemove.CompanyID].Status);
                // Different
                Assert.Equal(PaymentStatus.Different, secondDict[toModify.CompanyID].Status);
                // Unchanged
                var unchanged = updatedPayments
                                .Where(p => p.CompanyID != toModify.CompanyID)
                                .Select(p => p.CompanyID);
                foreach (var id in unchanged)
                    Assert.Equal(PaymentStatus.Unchanged, secondDict[id].Status);
            }
            finally
            {
                /* ---------- 6. Clean-up QB data ---------- */
                using var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME);

                // Payments
                foreach (var pay in firstCompareResult.Where(p => !string.IsNullOrEmpty(p.TxnID)))
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

            foreach (var p in firstCompareResult.Concat(secondCompareResult))
                Assert.Contains($"Payment {p.CompanyID} is {p.Status}.", logs);
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
