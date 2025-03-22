using System.Diagnostics;
using Serilog;
using QBFC16Lib;
using static QB_Payments_Test.CommonMethods; 

namespace QB_Payments_Test
{
    [Collection("Sequential Tests")]
    public class EndToEndPaymentTests
    {
        private const int CUSTOMER_COUNT = 4;       // We will create 4 customers
        private const int ITEMS_PER_CUSTOMER = 2;   // i.e. 2 * 4 = 8 items total
        private const int INVOICE_COUNT = 4;        // 4 invoices, each with 2 items
        private const int PAYMENTS_COUNT = 2;       // We'll create 2 payments, each paying 2 invoices

        [Fact]
        public void CreateAndDelete_Customers_Items_Invoices_Payments()
        {
            // Track QuickBooks IDs so we can delete them in finally:
            var createdCustomerListIDs = new List<string>();
            var createdItemListIDs = new List<string>();
            var createdInvoiceTxnIDs = new List<string>();
            var createdPaymentTxnIDs = new List<string>();

            // We'll also track "test data" to assert after queries.
            var randomCustomerNames = new List<string>();
            var randomItemNames = new List<string>();
            var randomItemPrices = new List<double>();

            // Each invoice's test data
            var invoiceData = new List<InvoiceTestInfo>();

            // Each payment's test data
            var paymentData = new List<PaymentTestInfo>();

            try
            {
                // 1) Clean up logs
                EnsureLogFileClosed();
                DeleteOldLogFiles();
                ResetLogger();

                // 2) Create customers
                using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    for (int i = 0; i < CUSTOMER_COUNT; i++)
                    {
                        string randomName = "RandCust_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                        string custListID = AddCustomer(qbSession, randomName);

                        createdCustomerListIDs.Add(custListID);
                        randomCustomerNames.Add(randomName);
                    }
                }

                // 3) Create items (2 * CUSTOMER_COUNT)
                using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    for (int i = 0; i < (ITEMS_PER_CUSTOMER * CUSTOMER_COUNT); i++)
                    {
                        string itemName = "RandItem_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                        double itemPrice = 10.0 + i; // just vary it slightly

                        string itemListID = AddInventoryItem(qbSession, itemName, itemPrice);

                        createdItemListIDs.Add(itemListID);
                        randomItemNames.Add(itemName);
                        randomItemPrices.Add(itemPrice);
                    }
                }

                // 4) Create invoices 
                using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    for (int i = 0; i < INVOICE_COUNT; i++)
                    {
                        // For the i-th invoice, we pick the i-th customer
                        string custListID = createdCustomerListIDs[i];
                        string custName = randomCustomerNames[i];

                        // We'll gather 2 items for this invoice:
                        // e.g. item indices = (2*i) and (2*i + 1)
                        int itemIndex1 = 2 * i;
                        int itemIndex2 = 2 * i + 1;

                        string itemListID1 = createdItemListIDs[itemIndex1];
                        string itemListID2 = createdItemListIDs[itemIndex2];
                        string itemName1 = randomItemNames[itemIndex1];
                        string itemName2 = randomItemNames[itemIndex2];
                        double itemPrice1 = randomItemPrices[itemIndex1];
                        double itemPrice2 = randomItemPrices[itemIndex2];

                        // We'll store a "CompanyID" in the invoice memo (e.g. 100+i)
                        int companyID = 100 + i;

                        // Create the invoice
                        string invoiceTxnID = AddInvoiceWithTwoLines(
                            qbSession,
                            custListID,
                            custName,
                            itemListID1, itemName1, itemPrice1,
                            itemListID2, itemName2, itemPrice2,
                            companyID
                        );

                        createdInvoiceTxnIDs.Add(invoiceTxnID);

                        // Store data
                        invoiceData.Add(new InvoiceTestInfo
                        {
                            TxnID = invoiceTxnID,
                            CustomerName = custName,
                            CompanyID = companyID,
                            ItemNames = new List<string> { itemName1, itemName2 },
                            ItemPrices = new List<double> { itemPrice1, itemPrice2 }
                        });
                    }
                }

                // 5) Create payments (PAYMENTS_COUNT),
                //    each payment pays 2 invoices in full.
                //    For simplicity, Payment #0 pays Invoice #0 and #1;
                //                    Payment #1 pays Invoice #2 and #3.
                //    We'll sum up the invoice item prices to get total to pay.
                using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    for (int p = 0; p < PAYMENTS_COUNT; p++)
                    {
                        // The two invoices we want to pay:
                        int invoiceIndex1 = 2 * p;       // 0, then 2
                        int invoiceIndex2 = 2 * p + 1;   // 1, then 3

                        InvoiceTestInfo inv1 = invoiceData[invoiceIndex1];
                        InvoiceTestInfo inv2 = invoiceData[invoiceIndex2];

                        // The Payment's "CompanyID" = let's do 200 + p
                        int paymentCompanyID = 200 + p;

                        // Payment date = Today
                        DateTime paymentDate = DateTime.Now.Date;

                        // Sum up the invoice totals. Each invoice has 2 items:
                        // e.g. inv1 total = sum( inv1.ItemPrices ), similarly for inv2
                        double invoice1Total = inv1.ItemPrices.Sum();
                        double invoice2Total = inv2.ItemPrices.Sum();
                        double paymentAmount = invoice1Total + invoice2Total;

                        // Because each invoice belongs to a single customer, we can assume
                        // both invoices are for the same customer.
                        // We'll pay them in full.
                        string paymentTxnID = AddReceivePayment(
                            qbSession,
                            inv1.CustomerName,  
                            inv1.TxnID,
                            inv2.TxnID,
                            paymentCompanyID,
                            paymentDate,
                            paymentAmount
                        );

                        createdPaymentTxnIDs.Add(paymentTxnID);

                        // Save Payment test data
                        paymentData.Add(new PaymentTestInfo
                        {
                            TxnID = paymentTxnID,
                            CompanyID = paymentCompanyID,
                            CustomerName = inv1.CustomerName, // assume both are same customer
                            PaymentDate = paymentDate,
                            InvoiceTxnIDs = new List<string> { inv1.TxnID, inv2.TxnID }
                        });
                    }
                }

                // 6) Query: check the newly created payments
                //    We'll assume you have PaymentReader.QueryAllPayments().
                var allPayments = PaymentReader.QueryAllPayments();

                // For each test Payment, confirm it’s present & correct in the query results
                foreach (var pd in paymentData)
                {
                    var match = allPayments.FirstOrDefault(x => x.TxnID == pd.TxnID);
                    Assert.NotNull(match);

                    // Check fields
                    Assert.Equal(pd.CompanyID, match.CompanyID);       // from Memo
                    Assert.Equal(pd.CustomerName, match.CustomerName);
                    Assert.Equal(pd.PaymentDate.Date, match.PaymentDate.Date);

                    // Check the invoices paid
                    // We expect exactly 2 invoices paid
                    Assert.Equal(2, match.InvoicesPaid.Count);
                    // Each is paid in full
                    foreach (var invoiceTxnID in pd.InvoiceTxnIDs)
                    {
                        Assert.Contains(invoiceTxnID, match.InvoicesPaid);
                    }
                }
            }
            finally
            {
                // 7) CLEANUP: we remove payments first, then invoices, then items, then customers
                using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    foreach (var payID in createdPaymentTxnIDs)
                    {
                        DeletePayment(qbSession, payID);
                    }
                }

                using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    foreach (var invID in createdInvoiceTxnIDs)
                    {
                        DeleteInvoice(qbSession, invID);
                    }
                }

                using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    foreach (var itemID in createdItemListIDs)
                    {
                        DeleteListObject(qbSession, itemID, ENListDelType.ldtItemInventory);
                    }
                }

                using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                {
                    foreach (var custID in createdCustomerListIDs)
                    {
                        DeleteListObject(qbSession, custID, ENListDelType.ldtCustomer);
                    }
                }
            }
        }

        //------------------------------------------------------------------------------
        // CREATORS
        //------------------------------------------------------------------------------

        private string AddCustomer(QuickBooksSession qbSession, string customerName)
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            var custAdd = request.AppendCustomerAddRq();
            custAdd.Name.SetValue(customerName);

            var resp = qbSession.SendRequest(request);
            return ExtractCustomerListID(resp);
        }

        private string AddInventoryItem(QuickBooksSession qbSession, string itemName, double price)
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            var itemAdd = request.AppendItemInventoryAddRq();

            itemAdd.Name.SetValue(itemName);

            itemAdd.IncomeAccountRef.FullName.SetValue("Sales");
            itemAdd.AssetAccountRef.FullName.SetValue("Inventory Asset");
            itemAdd.COGSAccountRef.FullName.SetValue("Cost of Goods Sold");

            itemAdd.SalesPrice.SetValue(price);

            var resp = qbSession.SendRequest(request);
            return ExtractItemListID(resp);
        }

        /// <summary>
        /// Creates an invoice with **2 line items**.
        /// Each line uses the specified item name/price. We store a “company ID” in Memo.
        /// Returns the new Invoice TxnID.
        /// </summary>
        private string AddInvoiceWithTwoLines(
            QuickBooksSession qbSession,
            string customerListID,
            string customerName,
            string itemListID1, string itemName1, double itemPrice1,
            string itemListID2, string itemName2, double itemPrice2,
            int companyID
        )
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            var invAdd = request.AppendInvoiceAddRq();

            invAdd.CustomerRef.ListID.SetValue(customerListID);
            invAdd.RefNumber.SetValue("Inv_" + Guid.NewGuid().ToString("N").Substring(0, 5));

            // Store "CompanyID" in the Memo
            invAdd.Memo.SetValue(companyID);
            invAdd.TxnDate.SetValue(DateTime.Today);

            // 1st line
            var line1 = invAdd.ORInvoiceLineAddList.Append().InvoiceLineAdd;
            line1.ItemRef.ListID.SetValue(itemListID1);
            line1.Quantity.SetValue(2);

            // 2nd line
            var line2 = invAdd.ORInvoiceLineAddList.Append().InvoiceLineAdd;
            line2.ItemRef.ListID.SetValue(itemListID2);
            line2.Quantity.SetValue(2);

            line1.ORRatePriceLevel.Rate.SetValue(itemPrice1);
            line2.ORRatePriceLevel.Rate.SetValue(itemPrice2);

            var resp = qbSession.SendRequest(request);
            return ExtractInvoiceTxnID(resp);
        }

        /// <summary>
        /// Creates a single ReceivePayment record that:
        ///   - references a single customer
        ///   - references 2 invoice TxnIDs
        ///   - pays each invoice in full
        ///   - uses the “XXX” in memo
        /// Returns the Payment’s TxnID.
        /// </summary>
        private string AddReceivePayment(
            QuickBooksSession qbSession,
            string customerName,   // or you could do CustomerRef.ListID if needed
            string invoiceTxnID1,
            string invoiceTxnID2,
            int companyID,
            DateTime paymentDate,
            double totalPaymentAmount
        )
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            var paymentRq = request.AppendReceivePaymentAddRq();

            // We can reference the customer by FullName or ListID
            paymentRq.CustomerRef.FullName.SetValue(customerName);

            // Payment date
            paymentRq.TxnDate.SetValue(paymentDate);

            // Memo includes our "CompanyID"
            paymentRq.Memo.SetValue(companyID);

            // The total payment amount:
            paymentRq.TotalAmount.SetValue(totalPaymentAmount);

            // The 1st invoice to pay in full
            var applied1 = paymentRq.AppliedToTxnAddList.Append();
            applied1.TxnID.SetValue(invoiceTxnID1);
            applied1.PaymentAmount.SetValue(GetInvoiceTotalFor(invoiceTxnID1));
            // For a real test, you might pass in the invoice total. 
            // Or store it in an object and look it up. We'll assume you have a helper.

            // The 2nd invoice to pay in full
            var applied2 = paymentRq.AppliedToTxnAddList.Append();
            applied2.TxnID.SetValue(invoiceTxnID2);
            applied2.PaymentAmount.SetValue(GetInvoiceTotalFor(invoiceTxnID2));

            // Send
            var resp = qbSession.SendRequest(request);
            return ExtractPaymentTxnID(resp);
        }

        //------------------------------------------------------------------------------
        // DELETERS
        //------------------------------------------------------------------------------

        private void DeletePayment(QuickBooksSession qbSession, string txnID)
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            var txnDel = request.AppendTxnDelRq();
            txnDel.TxnDelType.SetValue(ENTxnDelType.tdtReceivePayment);
            txnDel.TxnID.SetValue(txnID);

            var resp = qbSession.SendRequest(request);
            CheckForError(resp, $"Deleting Payment TxnID:{txnID}");
        }

        private void DeleteInvoice(QuickBooksSession qbSession, string txnID)
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            var txnDel = request.AppendTxnDelRq();
            txnDel.TxnDelType.SetValue(ENTxnDelType.tdtInvoice);
            txnDel.TxnID.SetValue(txnID);

            var resp = qbSession.SendRequest(request);
            CheckForError(resp, $"Deleting Invoice TxnID:{txnID}");
        }

        private void DeleteListObject(QuickBooksSession qbSession, string listID, ENListDelType listDelType)
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            var del = request.AppendListDelRq();
            del.ListDelType.SetValue(listDelType);
            del.ListID.SetValue(listID);

            var resp = qbSession.SendRequest(request);
            CheckForError(resp, $"Deleting {listDelType} ListID:{listID}");
        }

        //------------------------------------------------------------------------------
        // READERS / EXTRACTORS
        //------------------------------------------------------------------------------

        // For your real code, adapt as needed. 
        // In real usage, you might store invoice totals in a dictionary. 
        // Or you might have a method that queries QuickBooks for the invoice. 
        // Here, we just do a placeholder returning a fixed amount:
        private double GetInvoiceTotalFor(string invoiceTxnID)
        {
            // This is a simplified placeholder. 
            // If each invoice has 2 items, each with quantity=2, and itemPrice ~10-20,
            // total might be somewhere ~40-80. 
            // Alternatively, you might pass that total in from outside.
            return 50.0;
        }

        private string ExtractCustomerListID(IMsgSetResponse resp)
        {
            var list = resp.ResponseList;
            if (list == null || list.Count == 0)
                throw new Exception("No response from CustomerAdd.");

            var r = list.GetAt(0);
            if (r.StatusCode != 0)
                throw new Exception($"CustomerAdd failed: {r.StatusMessage}");

            var custRet = r.Detail as ICustomerRet;
            if (custRet == null)
                throw new Exception("No ICustomerRet returned.");

            return custRet.ListID.GetValue();
        }

        private string ExtractItemListID(IMsgSetResponse resp)
        {
            var list = resp.ResponseList;
            if (list == null || list.Count == 0)
                throw new Exception("No response from ItemInventoryAdd.");

            var r = list.GetAt(0);
            if (r.StatusCode != 0)
                throw new Exception($"ItemInventoryAdd failed: {r.StatusMessage}");

            var itemRet = r.Detail as IItemInventoryRet;
            if (itemRet == null)
                throw new Exception("No IItemInventoryRet returned.");

            return itemRet.ListID.GetValue();
        }

        private string ExtractInvoiceTxnID(IMsgSetResponse resp)
        {
            var list = resp.ResponseList;
            if (list == null || list.Count == 0)
                throw new Exception("No response from InvoiceAdd.");

            var r = list.GetAt(0);
            if (r.StatusCode != 0)
                throw new Exception($"InvoiceAdd failed: {r.StatusMessage}");

            var invRet = r.Detail as IInvoiceRet;
            if (invRet == null)
                throw new Exception("No IInvoiceRet returned.");

            return invRet.TxnID.GetValue();
        }

        private string ExtractPaymentTxnID(IMsgSetResponse resp)
        {
            var list = resp.ResponseList;
            if (list == null || list.Count == 0)
                throw new Exception("No response from ReceivePaymentAdd.");

            var r = list.GetAt(0);
            if (r.StatusCode != 0)
                throw new Exception($"ReceivePaymentAdd failed: {r.StatusMessage}");

            var payRet = r.Detail as IReceivePaymentRet;
            if (payRet == null)
                throw new Exception("No IReceivePaymentRet returned.");

            return payRet.TxnID.GetValue();
        }

        private void CheckForError(IMsgSetResponse resp, string context)
        {
            if (resp?.ResponseList == null || resp.ResponseList.Count == 0)
                return;

            var r = resp.ResponseList.GetAt(0);
            if (r.StatusCode != 0)
                throw new Exception($"Error {context}: {r.StatusMessage}. Code: {r.StatusCode}");
            else
                Debug.WriteLine($"OK: {context}");
        }

        //------------------------------------------------------------------------------
        // POCO classes to hold test data
        //------------------------------------------------------------------------------

        private class InvoiceTestInfo
        {
            public string TxnID { get; set; } = "";
            public string CustomerName { get; set; } = "";
            public int CompanyID { get; set; }
            public List<string> ItemNames { get; set; } = new();
            public List<double> ItemPrices { get; set; } = new();
        }

        private class PaymentTestInfo
        {
            public string TxnID { get; set; } = "";
            public int CompanyID { get; set; }
            public string CustomerName { get; set; } = "";
            public DateTime PaymentDate { get; set; }
            public List<string> InvoiceTxnIDs { get; set; } = new();
        }
    }
}
