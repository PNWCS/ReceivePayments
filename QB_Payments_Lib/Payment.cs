namespace QB_Payments_Lib
{
    public class Payment
    {
        public string TxnID { get; set; } = string.Empty;
        public int CompanyID { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public DateTime PaymentDate { get; set; }
        public List<string> InvoicesPaid { get; set; } = new();
        public decimal Amount { get; set; }
        public PaymentTermStatus Status { get; set; }

        // New properties to match the required variables
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal InoviceAmount { get; set; }
        public decimal BalanceRemaining { get; set; }
        public DateTime InvoiceDate { get; set; }
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