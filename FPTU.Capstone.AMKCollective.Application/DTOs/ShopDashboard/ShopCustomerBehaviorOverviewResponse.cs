namespace FPTU.Capstone.AMKCollective.Application.DTOs.ShopDashboard
{
    public class ShopCustomerBehaviorOverviewResponse
    {
        public DateTime FromUtc { get; set; }
        public DateTime ToUtc { get; set; }
        public int TotalCustomers { get; set; }
        public int NewCustomers { get; set; }
        public int ReturningCustomers { get; set; }
        public int RepeatCustomers { get; set; }
        public decimal RepeatRate { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal PurchaseFrequency { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal NetRevenue { get; set; }
    }
}
