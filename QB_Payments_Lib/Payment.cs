namespace QB_Payments_Lib
{
    public class Payment
    {
        //public string CustomerFullName { get; set; }
        //public DateTime TxnDate { get; set; }
        //public string RefNumber { get; set; }
        //public double TotalAmount { get; set; }


        public string TxnID { get; set; } = "";
        public int CompanyID { get; set; }
        public string CustomerName { get; set; } = "";
        public DateTime PaymentDate { get; set; }
        public List<string> InvoicesPaid { get; set; } = new List<string>();
        //public int invoices
    }
}
