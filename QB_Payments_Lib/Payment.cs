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
        public PaymentTermStatus Status { get; set; } = PaymentTermStatus.Unknown;


    }
}

public enum PaymentTermStatus
{
    Unknown,       // When first read from the company excel or QB
    Unchanged,     // Exists in both but no changes
    Different,     // Exists in both but name is different
    Added,         // Newly added to QB
    FailedToAdd,   // If adding to QB failed
    Missing        // Exists in QB but not in the company file
}