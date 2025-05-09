namespace QB_Payments_Lib
{
    public class Customer
    {
        public string Name { get; set; }
        public string CompanyName { get; set; }
        public string QB_ID { get; set; } // QuickBooks ID

        public Customer(string name, string companyName)
        {
            Name = name;
            CompanyName = companyName;
            QB_ID = string.Empty; // Initialize QB_ID as empty
        }
    }
}
