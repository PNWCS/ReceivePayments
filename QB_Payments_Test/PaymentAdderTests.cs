// QB_Payments_Test/PaymentAdderTests.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using QBFC16Lib;
using QB_Payments_Lib;          // Payment model + PaymentAdder
using static QB_Payments_Test.CommonMethods;
using Xunit;

namespace QB_Payments_Test
{
    [Collection("Sequential Tests")]   // make sure we hold the company file lock
    public class PaymentAdderTests
    {
        // ------------------------------------------------------------------
        // CONFIGURATION
        // ------------------------------------------------------------------
        private const int CUSTOMER_COUNT         = 3;  // customers to create
        private const int ITEMS_PER_CUSTOMER     = 2;  // inventory items per customer
        private const int INVOICES_PER_CUSTOMER  = 2;  // each customer gets 2 invoices
        private const string SALES_ACCOUNT       = "Sales";
        private const string INVENTORY_ACCOUNT   = "Inventory Asset";
        private const string COGS_ACCOUNT        = "Cost of Goods Sold";

        // ------------------------------------------------------------------
        // MAIN TEST
        // ------------------------------------------------------------------
        [Fact]
        public void AddPayments_MultipleCustomers_MultiInvoicePayments_Succeeds()
        {
            //-----------------------------------------------------------------
            // Tracking collections so we can assert and (optionally) clean up
            //-----------------------------------------------------------------
            var createdCustomerListIDs = new List<string>();
            var createdItemListIDs     = new List<string>();
            var createdInvoiceTxnIDs   = new List<string>();
            var addedPaymentTxnIDs     = new List<string>();

            var randomCustomerNames    = new List<string>();
            var invoiceInfo            = new List<InvoiceInfo>();   // used to build payments

            //-----------------------------------------------------------------
            // STEP 1 – log cleanup
            //-----------------------------------------------------------------
            EnsureLogFileClosed();
            DeleteOldLogFiles();
            ResetLogger();

            //-----------------------------------------------------------------
            // STEP 2 – create CUSTOMERS
            //-----------------------------------------------------------------
            for (int i = 0; i < CUSTOMER_COUNT; i++)
            {
                using var qb = new QuickBooksSession(AppConfig.QB_APP_NAME);

                string custName  = "Cust_" + Guid.NewGuid().ToString("N")[..6];
                string custListID = AddCustomer(qb, custName);

                randomCustomerNames.Add(custName);
                createdCustomerListIDs.Add(custListID);
            }

            //-----------------------------------------------------------------
            // STEP 3 – create INVENTORY ITEMS
            //-----------------------------------------------------------------
            for (int i = 0; i < CUSTOMER_COUNT * ITEMS_PER_CUSTOMER; i++)
            {
                using var qb = new QuickBooksSession(AppConfig.QB_APP_NAME);

                string itemName  = "Item_" + Guid.NewGuid().ToString("N")[..6];
                double price     = 10.0 + i;      // just something distinct
                string itemListID = AddInventoryItem(qb, itemName, price);

                createdItemListIDs.Add(itemListID);
            }

            //-----------------------------------------------------------------
            // STEP 4 – create INVOICES
            //-----------------------------------------------------------------
            for (int cust = 0; cust < CUSTOMER_COUNT; cust++)
            {
                for (int inv = 0; inv < INVOICES_PER_CUSTOMER; inv++)
                {
                    using var qb = new QuickBooksSession(AppConfig.QB_APP_NAME);

                    // grab two items, wrap if we run out
                    int itemBase = (cust * ITEMS_PER_CUSTOMER) % createdItemListIDs.Count;
                    string itemListID1 = createdItemListIDs[itemBase];
                    string itemListID2 = createdItemListIDs[(itemBase + 1) % createdItemListIDs.Count];

                    string custListID  = createdCustomerListIDs[cust];
                    string custName    = randomCustomerNames[cust];
                    int companyID      = 1000 + cust * 10 + inv;

                    string invoiceTxnID = AddInvoiceWithTwoLines(
                        qb,
                        custListID, custName,
                        itemListID1, "ItemA", 15.25,
                        itemListID2, "ItemB", 17.40,
                        companyID
                    );

                    createdInvoiceTxnIDs.Add(invoiceTxnID);
                    invoiceInfo.Add(new InvoiceInfo
                    {
                        CustomerName = custName,
                        TxnID        = invoiceTxnID
                    });
                }
            }

            //-----------------------------------------------------------------
            // STEP 5 – build Payment objects
            //-----------------------------------------------------------------
            var paymentsToAdd = new List<Payment>();

            foreach (string custName in randomCustomerNames)
            {
                // The invoices for this customer
                var custInvoices = invoiceInfo
                    .Where(i => i.CustomerName == custName)
                    .Select(i => i.TxnID)
                    .ToList();

                if (custInvoices.Count == 0) continue;

                // get total $ due so we can populate Amount
                double totalDue = 0;
                using (var qb = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    foreach (string invTxn in custInvoices)
                        totalDue += GetInvoiceAmountDue(qb, invTxn);
                }

                paymentsToAdd.Add(new Payment
                {
                    CompanyID    = 2000 + paymentsToAdd.Count,
                    CustomerName = custName,
                    PaymentDate  = DateTime.Today,
                    InvoicesPaid = custInvoices,
                    Amount       = (decimal)totalDue         // pay everything in full
                });
            }

            //-----------------------------------------------------------------
            // STEP 6 – call the code under test
            //-----------------------------------------------------------------
            PaymentAdder.AddPayments(paymentsToAdd);

            // give QB a breath in case AddPayments spins up multiple sessions
            Thread.Sleep(1000);

            //-----------------------------------------------------------------
            // STEP 7 – assertions
            //-----------------------------------------------------------------
            foreach (var p in paymentsToAdd)
            {
                // 7‑A: TxnID must be set
                Assert.False(string.IsNullOrWhiteSpace(p.TxnID),
                    $"Payment for CompanyID={p.CompanyID} did not get a TxnID.");

                addedPaymentTxnIDs.Add(p.TxnID);

                // 7‑B: Payment must actually exist inside QB
                Assert.True(DoesPaymentExistInQB(p.TxnID),
                    $"Payment TxnID {p.TxnID} was not found in QuickBooks.");
            }

            //-----------------------------------------------------------------
            // STEP 8 – optional clean‑up (commented for forensic debugging)
            //-----------------------------------------------------------------
            Cleanup(createdCustomerListIDs, createdItemListIDs,
                    createdInvoiceTxnIDs, addedPaymentTxnIDs);
        }

        // ------------------------------------------------------------------
        // QB HELPER METHODS  (Add / Query / Delete)
        // ------------------------------------------------------------------

        private string AddCustomer(QuickBooksSession qb, string name)
        {
            var req = qb.CreateRequestSet();
            var add = req.AppendCustomerAddRq();
            add.Name.SetValue(name);
            var resp = qb.SendRequest(req);
            return ((ICustomerRet)resp.ResponseList.GetAt(0).Detail).ListID.GetValue();
        }

        private string AddInventoryItem(QuickBooksSession qb, string name, double price)
        {
            var req  = qb.CreateRequestSet();
            var add  = req.AppendItemInventoryAddRq();
            add.Name.SetValue(name);
            add.IncomeAccountRef.FullName.SetValue(SALES_ACCOUNT);
            add.AssetAccountRef.FullName.SetValue(INVENTORY_ACCOUNT);
            add.COGSAccountRef.FullName.SetValue(COGS_ACCOUNT);
            add.SalesPrice.SetValue(price);
            var resp = qb.SendRequest(req);
            return ((IItemInventoryRet)resp.ResponseList.GetAt(0).Detail).ListID.GetValue();
        }

        private string AddInvoiceWithTwoLines(
            QuickBooksSession qb,
            string custListID, string custName,
            string itemListID1, string itemName1, double itemPrice1,
            string itemListID2, string itemName2, double itemPrice2,
            int companyID)
        {
            var req  = qb.CreateRequestSet();
            var inv  = req.AppendInvoiceAddRq();
            inv.CustomerRef.ListID.SetValue(custListID);
            inv.Memo.SetValue(companyID.ToString());
            inv.TxnDate.SetValue(DateTime.Today);
            inv.RefNumber.SetValue("INV_" + Guid.NewGuid().ToString("N")[..6]);

            var l1 = inv.ORInvoiceLineAddList.Append().InvoiceLineAdd;
            l1.ItemRef.ListID.SetValue(itemListID1);
            l1.Quantity.SetValue(1);
            l1.ORRatePriceLevel.Rate.SetValue(itemPrice1);

            var l2 = inv.ORInvoiceLineAddList.Append().InvoiceLineAdd;
            l2.ItemRef.ListID.SetValue(itemListID2);
            l2.Quantity.SetValue(1);
            l2.ORRatePriceLevel.Rate.SetValue(itemPrice2);

            var resp = qb.SendRequest(req);
            return ((IInvoiceRet)resp.ResponseList.GetAt(0).Detail).TxnID.GetValue();
        }

        private double GetInvoiceAmountDue(QuickBooksSession qb, string invoiceTxnID)
        {
            var req = qb.CreateRequestSet();
            var q   = req.AppendInvoiceQueryRq();
            q.ORInvoiceQuery.TxnIDList.Add(invoiceTxnID);
            var resp = qb.SendRequest(req);
            var ret  = ((IInvoiceRetList)resp.ResponseList.GetAt(0).Detail).GetAt(0);
            return ret.BalanceRemaining.GetValue();
        }

        /// <summary>
        /// Simple point‑query by TxnID.  Returns TRUE if QB returns exactly one ReceivePaymentRet.
        /// </summary>
        private bool DoesPaymentExistInQB(string txnID)
        {
            using var qb = new QuickBooksSession(AppConfig.QB_APP_NAME);

            var req  = qb.CreateRequestSet();
            var qry  = req.AppendReceivePaymentQueryRq();
            qry.ORTxnQuery.TxnIDList.Add(txnID);

            var resp = qb.SendRequest(req);
            if (resp.ResponseList.Count == 0) return false;

            var r = resp.ResponseList.GetAt(0);
            if (r.StatusCode != 0) return false;

            var list = r.Detail as IReceivePaymentRetList;
            return list != null && list.Count == 1;
        }

        // ------------------------------------------------------------------
        // (Optional) bulk delete helpers so the company file stays clean
        // ------------------------------------------------------------------
        private void Cleanup(
            IEnumerable<string> customerListIDs,
            IEnumerable<string> itemListIDs,
            IEnumerable<string> invoiceTxnIDs,
            IEnumerable<string> paymentTxnIDs)
        {
            foreach (string p in paymentTxnIDs)
                using (var qb = new QuickBooksSession(AppConfig.QB_APP_NAME))
                    DeleteTxn(qb, p, ENTxnDelType.tdtReceivePayment);

            foreach (string i in invoiceTxnIDs)
                using (var qb = new QuickBooksSession(AppConfig.QB_APP_NAME))
                    DeleteTxn(qb, i, ENTxnDelType.tdtInvoice);

            foreach (string id in itemListIDs)
                using (var qb = new QuickBooksSession(AppConfig.QB_APP_NAME))
                    DeleteList(qb, id, ENListDelType.ldtItemInventory);

            foreach (string c in customerListIDs)
                using (var qb = new QuickBooksSession(AppConfig.QB_APP_NAME))
                    DeleteList(qb, c, ENListDelType.ldtCustomer);
        }

        private void DeleteTxn(QuickBooksSession qb, string txnID, ENTxnDelType type)
        {
            var req = qb.CreateRequestSet();
            var del = req.AppendTxnDelRq();
            del.TxnDelType.SetValue(type);
            del.TxnID.SetValue(txnID);
            qb.SendRequest(req);
        }

        private void DeleteList(QuickBooksSession qb, string listID, ENListDelType type)
        {
            var req = qb.CreateRequestSet();
            var del = req.AppendListDelRq();
            del.ListDelType.SetValue(type);
            del.ListID.SetValue(listID);
            qb.SendRequest(req);
        }

        // ------------------------------------------------------------------
        //  Small POCOs for internal bookkeeping
        // ------------------------------------------------------------------
        private sealed class InvoiceInfo
        {
            public string CustomerName { get; set; } = "";
            public string TxnID        { get; set; } = "";
        }
    }
}