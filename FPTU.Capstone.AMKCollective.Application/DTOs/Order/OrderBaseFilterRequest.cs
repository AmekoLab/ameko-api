namespace FPTU.Capstone.AMKCollective.Application.DTOs.Order
{
    public class OrderBaseFilterRequest
    {
        public int Page { get; set; } = 1;
        public int Size { get; set; } = 10;
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}
