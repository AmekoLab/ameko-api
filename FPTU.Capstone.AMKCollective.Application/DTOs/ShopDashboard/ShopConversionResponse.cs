namespace FPTU.Capstone.AMKCollective.Application.DTOs.ShopDashboard
{
    public class ShopConversionResponse
    {
        public DateTime FromUtc { get; set; }
        public DateTime ToUtc { get; set; }
        public int TotalOrders { get; set; }
        public int PaidOrders { get; set; }
        public int CompletedOrders { get; set; }
        public int CancelledOrders { get; set; }
        public decimal PaidRate { get; set; }
        public decimal CompletionRate { get; set; }
        public decimal CancelRate { get; set; }
    }
}
