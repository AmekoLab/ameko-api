namespace FPTU.Capstone.AMKCollective.Application.DTOs.AdminDashboard
{
    public class AdminDashboardOverviewResponse
    {
        public DateTime FromUtc { get; set; }
        public DateTime ToUtc { get; set; }

        public int TotalOrders { get; set; }
        public int CompletedOrders { get; set; }
        public int CancelledOrders { get; set; }
        public int RefundedOrders { get; set; }

        public decimal GrossMerchandiseValue { get; set; }
        public decimal NetRevenue { get; set; }
        public decimal PlatformRevenue { get; set; }

        public int ActiveBuyers { get; set; }
        public int ActiveShops { get; set; }
        public int NewUsers { get; set; }

        public decimal OrderCompletionRate { get; set; }
    }
}
