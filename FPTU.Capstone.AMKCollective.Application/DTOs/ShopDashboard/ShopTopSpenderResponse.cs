namespace FPTU.Capstone.AMKCollective.Application.DTOs.ShopDashboard
{
    public class ShopTopSpenderResponse
    {
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int Orders { get; set; }
        public decimal TotalSpent { get; set; }
        public DateTime LastOrderAtUtc { get; set; }
    }
}
