namespace FPTU.Capstone.AMKCollective.Application.DTOs.ShopDashboard
{
    public class ShopPurchaseFrequencyResponse
    {
        public DateTime FromUtc { get; set; }
        public DateTime ToUtc { get; set; }
        public int CustomersWithOrders { get; set; }
        public int TotalOrders { get; set; }
        public decimal OrdersPerCustomer { get; set; }
        public decimal AverageDaysBetweenOrders { get; set; }
    }
}
