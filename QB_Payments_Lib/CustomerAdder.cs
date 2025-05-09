using QBFC16Lib;

namespace QB_Payments_Lib
{
    public static class CustomerAdder
    {
        public static void AddCustomers(List<Customer> customers)
        {
            using var qbSession = new QuickBooksSession(AppConfig.QB_APP_NAME);
            foreach (var customer in customers)
            {
                IMsgSetRequest request = qbSession.CreateRequestSet();
                ICustomerAdd customerAddRq = request.AppendCustomerAddRq();
                customerAddRq.Name.SetValue(customer.Name);
                customerAddRq.CompanyName.SetValue(customer.CompanyName);

                IMsgSetResponse response = qbSession.SendRequest(request);
                IResponse qbResponse = response.ResponseList.GetAt(0);
                if (qbResponse.StatusCode == 0 && qbResponse.Detail is ICustomerRet customerRet)
                {
                    customer.QB_ID = customerRet.ListID.GetValue();
                }
            }
        }
    }
}
