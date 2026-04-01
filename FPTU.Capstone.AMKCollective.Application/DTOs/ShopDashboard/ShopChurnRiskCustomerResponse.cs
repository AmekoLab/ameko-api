namespace FPTU.Capstone.AMKCollective.Application.DTOs.ShopDashboard
{
    public class ShopChurnRiskCustomerResponse
    {
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int LifetimeOrders { get; set; }
        public decimal LifetimeValue { get; set; }
        public DateTime LastOrderAtUtc { get; set; }
        public int InactiveDays { get; set; }
    }
}
