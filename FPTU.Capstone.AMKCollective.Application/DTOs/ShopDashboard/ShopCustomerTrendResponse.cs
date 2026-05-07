namespace FPTU.Capstone.AMKCollective.Application.DTOs.ShopDashboard
{
    public class ShopCustomerTrendResponse
    {
        public DateTime BucketStartUtc { get; set; }
        public int NewCustomers { get; set; }
        public int ReturningCustomers { get; set; }
        public int Orders { get; set; }
        public decimal Revenue { get; set; }
        public decimal NetRevenue { get; set; }
    }
}
