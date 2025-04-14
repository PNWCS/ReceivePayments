namespace QB_Payments_Lib
{
    public class Payment
    {
        public string TxnID { get; set; } = "";
        public int CompanyID { get; set; }
        public string CustomerName { get; set; } = "";
        public DateTime PaymentDate { get; set; }
        public List<string> InvoicesPaid { get; set; } = new List<string>();
        public decimal Amount { get; set; } // New field to specify the amount to be paid
    }
}
