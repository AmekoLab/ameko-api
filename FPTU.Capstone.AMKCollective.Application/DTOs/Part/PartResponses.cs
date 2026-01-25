namespace FPTU.Capstone.AMKCollective.Application.DTOs.Part
{
    /// <summary>
    /// DTO thông tin Part đầy đủ
    /// </summary>
    public class PartDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string PartType { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string? DefaultLayerImageUrl { get; set; }
        public string? Description { get; set; }
        public string? Specifications { get; set; } // JSON

        public Guid ShopId { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO Part trong danh sách category
    /// </summary>
    public class PartInCategoryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ThumbnailURL { get; set; }
        public string PartType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int StockQuantity { get; set; }
        public Guid ShopId { get; set; }
        public string? ShopName { get; set; }
    }
}
