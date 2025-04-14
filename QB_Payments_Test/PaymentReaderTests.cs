using System.Diagnostics;
using QB_Payments_Lib;
using QBFC16Lib;
using static QB_Payments_Test.CommonMethods;

namespace QB_Payments_Test
{
    [Collection("Sequential Tests")]
    public class PaymentReaderTests
    {
        // Configuration constants
        private const int CUSTOMER_COUNT = 4;       // We will create 4 customers
        private const int ITEMS_PER_CUSTOMER = 2;   // i.e. 2 * 4 = 8 items total
        private const int INVOICE_COUNT = 4;        // 4 invoices, each with 2 items
        private const int PAYMENTS_COUNT = 4;       // We'll create 2 payments, each paying 2 invoices

        [Fact]
        public void CreateAndDelete_Multiple_Customers_Invoices_Payments()
        {
            // Track QuickBooks IDs so we can delete them in finally:
            List<string> createdCustomerListIDs = new List<string>();
            List<string> createdItemListIDs = new List<string>();
            List<string> createdInvoiceTxnIDs = new List<string>();
            List<string> createdPaymentTxnIDs = new List<string>();

            // We'll also track "test data" to assert after queries.
            List<string> randomCustomerNames = new List<string>();
            List<string> randomItemNames = new List<string>();
            List<double> randomItemPrices = new List<double>();

            // Invoice test data
            List<InvoiceTestInfo> invoiceData = new List<InvoiceTestInfo>();

            // Payment test data
            List<PaymentTestInfo> paymentData = new List<PaymentTestInfo>();

            try
            {
                // 1) Clean up logs
                EnsureLogFileClosed();
                DeleteOldLogFiles();
                ResetLogger();

                // 2) Create customers
                for (int i = 0; i < CUSTOMER_COUNT; i++)
                {
                    using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                    {
                        string randomCustomerName = "RandCust987_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                        string customerListID = AddCustomer(qbSession, randomCustomerName);

                        randomCustomerNames.Add(randomCustomerName);
                        createdCustomerListIDs.Add(customerListID);
                    }
                }

                // 3) Create items
                for (int i = 0; i < CUSTOMER_COUNT * ITEMS_PER_CUSTOMER; i++)
                {
                    using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                    {
                        string randomItemName = "RandItem987_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                        double randomItemPrice = 10.0 + (i * 2.5); // Different prices for variety
                        string itemListID = AddInventoryItem(qbSession, randomItemName, randomItemPrice);

                        randomItemNames.Add(randomItemName);
                        randomItemPrices.Add(randomItemPrice);
                        createdItemListIDs.Add(itemListID);
                    }
                }

                // 4) Create invoices - one invoice per customer, with two line items each
                for (int i = 0; i < INVOICE_COUNT; i++)
                {
                    using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                    {
                        // Get the customer for this invoice (cycling through customers if needed)
                        int customerIndex = i % CUSTOMER_COUNT;
                        string customerListID = createdCustomerListIDs[customerIndex];
                        string customerName = randomCustomerNames[customerIndex];

                        // Get two items for this invoice
                        int itemIndex1 = (i * 2) % (CUSTOMER_COUNT * ITEMS_PER_CUSTOMER);
                        int itemIndex2 = (i * 2 + 1) % (CUSTOMER_COUNT * ITEMS_PER_CUSTOMER);

                        // Store a "CompanyID" in the invoice memo (e.g. 100 + invoice number)
                        int companyID = 100 + i;

                        // Create the invoice
                        string invoiceTxnID = AddInvoiceWithTwoLines(
                            qbSession,
                            customerListID,
                            customerName,
                            createdItemListIDs[itemIndex1], randomItemNames[itemIndex1], randomItemPrices[itemIndex1],
                            createdItemListIDs[itemIndex2], randomItemNames[itemIndex2], randomItemPrices[itemIndex2],
                            companyID
                        );

                        createdInvoiceTxnIDs.Add(invoiceTxnID);

                        // Store data
                        invoiceData.Add(new InvoiceTestInfo
                        {
                            TxnID = invoiceTxnID,
                            CustomerName = customerName,
                            CompanyID = companyID,
                            ItemNames = new List<string> { randomItemNames[itemIndex1], randomItemNames[itemIndex2] },
                            ItemPrices = new List<double> { randomItemPrices[itemIndex1], randomItemPrices[itemIndex2] }
                        });
                    }
                }

                // 5) Create payments - each payment covers multiple invoices from the same customer
                for (int i = 0; i < PAYMENTS_COUNT; i++)
                {
                    using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                    {
                        // For simplicity, each payment will pay invoices for a single customer
                        // Payment 1 pays first half of invoices, Payment 2 pays second half
                        int startInvoiceIdx = i * (INVOICE_COUNT / PAYMENTS_COUNT);
                        int endInvoiceIdx = (i + 1) * (INVOICE_COUNT / PAYMENTS_COUNT) - 1;

                        // Get the customer for the first invoice in this batch
                        string customerName = invoiceData[startInvoiceIdx].CustomerName;

                        // Get all invoices for this customer in the batch
                        List<string> invoicesToPay = new List<string>();
                        double totalPaymentAmount = 0;

                        for (int j = startInvoiceIdx; j <= endInvoiceIdx; j++)
                        {
                            if (invoiceData[j].CustomerName == customerName)
                            {
                                invoicesToPay.Add(invoiceData[j].TxnID);
                                // Sum up the invoice amounts
                                totalPaymentAmount += invoiceData[j].ItemPrices.Sum() * 2; // x2 because quantity=2 in AddInvoiceWithTwoLines
                            }
                        }

                        // The Payment's "CompanyID" = 200 + payment index
                        int paymentCompanyID = 200 + i;

                        // Payment date = Today + payment index days
                        DateTime paymentDate = DateTime.Now.Date.AddDays(i);

                        // Create the payment covering all invoices for this customer
                        string paymentTxnID = AddReceivePaymentMultipleInvoices(
                            qbSession,
                            customerName,
                            invoicesToPay,
                            paymentCompanyID,
                            paymentDate,
                            totalPaymentAmount
                        );

                        createdPaymentTxnIDs.Add(paymentTxnID);

                        // Store Payment test data
                        paymentData.Add(new PaymentTestInfo
                        {
                            TxnID = paymentTxnID,
                            CompanyID = paymentCompanyID,
                            CustomerName = customerName,
                            PaymentDate = paymentDate,
                            InvoiceTxnIDs = invoicesToPay
                        });
                    }
                }

                // 6) Query: check all created payments
                var allPayments = PaymentReader.QueryAllPayments();

                // Verify each payment
                foreach (var expectedPayment in paymentData)
                {
                    var match = allPayments.FirstOrDefault(x => x.TxnID == expectedPayment.TxnID);
                    Assert.NotNull(match);

                    // Check fields
                    Assert.Equal(expectedPayment.CompanyID, match.CompanyID);    // from Memo
                    Assert.Equal(expectedPayment.CustomerName, match.CustomerName);
                    Assert.Equal(expectedPayment.PaymentDate.Date, match.PaymentDate.Date);

                    // Check the invoices paid
                    foreach (var invoiceTxnID in expectedPayment.InvoiceTxnIDs)
                    {
                        Assert.Contains(invoiceTxnID, match.InvoicesPaid);
                    }
                }
            }
            finally
            {
                // 7) CLEANUP: we remove payments first, then invoices, then items, then customers
                // Delete payments
                //foreach (var paymentTxnID in createdPaymentTxnIDs)
                //{
                //    using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                //    {
                //        DeletePayment(qbSession, paymentTxnID);
                //    }
                //}

                //// Delete invoices
                //foreach (var invoiceTxnID in createdInvoiceTxnIDs)
                //{
                //    using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                //    {
                //        DeleteInvoice(qbSession, invoiceTxnID);
                //    }
                //}

                //// Delete items
                //foreach (var itemListID in createdItemListIDs)
                //{
                //    using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                //    {
                //        DeleteListObject(qbSession, itemListID, ENListDelType.ldtItemInventory);
                //    }
                //}

                //// Delete customers
                //foreach (var customerListID in createdCustomerListIDs)
                //{
                //    using (var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME))
                //    {
                //        DeleteListObject(qbSession, customerListID, ENListDelType.ldtCustomer);
                //    }
                //}
            }
        }


        private double GetInvoiceAmountDue(QuickBooksSession qbSession, string invoiceTxnID)
        {
            IMsgSetRequest request = qbSession.CreateRequestSet();
            var invoiceQuery = request.AppendInvoiceQueryRq();
            invoiceQuery.ORInvoiceQuery.TxnIDList.Add(invoiceTxnID);

            var resp = qbSession.SendRequest(request);
            var list = resp.ResponseList;
            if (list == null || list.Count == 0)
                throw new Exception($"No response from InvoiceQuery for TxnID: {invoiceTxnID}");

            var r = list.GetAt(0);
            if (r.StatusCode != 0)
                throw new Exception($"InvoiceQuery failed: {r.StatusMessage}");

            var invoiceRetList = r.Detail as IInvoiceRetList;
            if (invoiceRetList == null || invoiceRetList.Count == 0)
                throw new Exception($"No IInvoiceRet returned for TxnID: {invoiceTxnID}");

            var invoiceRet = invoiceRetList.GetAt(0);
            return invoiceRet.BalanceRemaining.GetValue();
        }


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
        /// Each line uses the specified item name/price. We store a "company ID" in Memo.
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
            invAdd.Memo.SetValue(companyID.ToString());
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
        ///   - references a single invoice TxnID
        ///   - pays the invoice in full
        ///   - uses the companyID in memo
        /// Returns the Payment's TxnID.
        /// </summary>
        private string AddReceivePayment(
            QuickBooksSession qbSession,
            string customerName,   // or you could do CustomerRef.ListID if needed
            string invoiceTxnID,
            int companyID,
            DateTime paymentDate,
            double totalPaymentAmount
        )
        {
            // Retrieve the amount due for the invoice
            double amountDue = GetInvoiceAmountDue(qbSession, invoiceTxnID);

            IMsgSetRequest request = qbSession.CreateRequestSet();
            var paymentRq = request.AppendReceivePaymentAddRq();

            // We can reference the customer by FullName or ListID
            paymentRq.CustomerRef.FullName.SetValue(customerName);

            // Payment date
            paymentRq.TxnDate.SetValue(paymentDate);

            // Memo includes our "CompanyID"
            paymentRq.Memo.SetValue(companyID.ToString());

            // The total payment amount:
            paymentRq.TotalAmount.SetValue(Math.Min(totalPaymentAmount, amountDue));

            // The invoice to pay in full
            var applied = paymentRq.ORApplyPayment.AppliedToTxnAddList.Append();
            applied.TxnID.SetValue(invoiceTxnID);
            applied.PaymentAmount.SetValue(Math.Min(totalPaymentAmount, amountDue));

            // Send
            var resp = qbSession.SendRequest(request);
            return ExtractPaymentTxnID(resp);
        }

        /// <summary>
        /// Creates a single ReceivePayment record that:
        ///   - references a single customer
        ///   - references multiple invoice TxnIDs
        ///   - pays each invoice in full
        ///   - uses the companyID in memo
        /// Returns the Payment's TxnID.
        /// </summary>
        private string AddReceivePaymentMultipleInvoices(
            QuickBooksSession qbSession,
            string customerName,
            List<string> invoiceTxnIDs,
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
            paymentRq.Memo.SetValue(companyID.ToString());

            // Calculate the total payment amount by summing invoice amounts due
            double totalAmountDue = 0;
            Dictionary<string, double> invoiceAmountsDue = new Dictionary<string, double>();

            foreach (var invoiceTxnID in invoiceTxnIDs)
            {
                double amountDue = GetInvoiceAmountDue(qbSession, invoiceTxnID);
                invoiceAmountsDue[invoiceTxnID] = amountDue;
                totalAmountDue += amountDue;
            }

            // The total payment amount (limited by the provided amount)
            double actualPaymentAmount = Math.Min(totalPaymentAmount, totalAmountDue);
            paymentRq.TotalAmount.SetValue(actualPaymentAmount);

            // If we can't pay all invoices in full, we'll need to allocate the payment
            double remainingPayment = actualPaymentAmount;

            // Apply payment to each invoice
            foreach (var invoiceTxnID in invoiceTxnIDs)
            {
                double amountDue = invoiceAmountsDue[invoiceTxnID];
                double paymentForThisInvoice = Math.Min(remainingPayment, amountDue);

                if (paymentForThisInvoice > 0)
                {
                    var applied = paymentRq.ORApplyPayment.AppliedToTxnAddList.Append();
                    applied.TxnID.SetValue(invoiceTxnID);
                    applied.PaymentAmount.SetValue(paymentForThisInvoice);

                    remainingPayment -= paymentForThisInvoice;
                }

                // If we've used up all the payment amount, stop applying
                if (remainingPayment <= 0)
                    break;
            }

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