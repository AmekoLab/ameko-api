namespace FPTU.Capstone.AMKCollective.Application.DTOs.AdminDashboard
{
    public class AdminTopShopsByOrdersResponse
    {
        public DateTime FromUtc { get; set; }
        public DateTime ToUtc { get; set; }
        public List<AdminTopShopOrderItem> Items { get; set; } = new();
    }

    public class AdminTopShopOrderItem
    {
        public Guid ShopId { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public int OrderCount { get; set; }
    }
}
