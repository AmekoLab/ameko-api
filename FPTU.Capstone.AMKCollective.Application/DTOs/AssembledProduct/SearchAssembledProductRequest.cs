namespace FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProduct
{
    public class SearchAssembledProductRequest
    {
        public string? SearchTerm { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? Layout { get; set; }
        public string? Mounting { get; set; }
        public string? PCB { get; set; }
        public string? Connection { get; set; }
        public string? Battery { get; set; }
        public Guid? ShopId { get; set; }
        public double? MinRating { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
