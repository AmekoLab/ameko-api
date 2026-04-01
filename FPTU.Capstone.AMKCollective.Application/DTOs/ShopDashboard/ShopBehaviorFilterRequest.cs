namespace FPTU.Capstone.AMKCollective.Application.DTOs.ShopDashboard
{
    public class ShopBehaviorFilterRequest
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int ChurnDays { get; set; } = 30;
        public string Granularity { get; set; } = "day";
    }
}
