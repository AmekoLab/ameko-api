

using Microsoft.AspNetCore.Http;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Part
{
    /// <summary>
    /// Request tạo/cập nhật Part (form-data)
    /// </summary>
    public class CreateUpdatePartRequest
    {
        public Guid CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PartType { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public string? Description { get; set; }
        public string? Specifications { get; set; } // JSON string

        public int? RecipeSwitchCount { get; set; }
        public int? RecipeStabilizerCount { get; set; }

        public IFormFile? ThumbnailImage { get; set; }
        public IFormFile? LayerImage { get; set; }
    }

    /// <summary>
    /// Query params cho danh sách Part
    /// </summary>
    public class PartQueryParams
    {
        public Guid? ShopId { get; set; }
        public Guid CategoryId { get; set; }
        public string? PartType { get; set; }
        public bool? IsActive { get; set; }
        public string? SearchTerm { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    /// <summary>
    /// Request kiểm tra tồn kho sản phẩm
    /// </summary>
    public class CheckStockRequest
    {
        public List<Guid> ProductIds { get; set; } = new List<Guid>();
    }
}
