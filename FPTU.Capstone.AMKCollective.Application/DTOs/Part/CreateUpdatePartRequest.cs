using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Part
{
    public class CreateUpdatePartRequest
    {
        public Guid CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PartType { get; set; } = string.Empty;

        /// True = part này hiển thị trong addon-options picker (bàn phím ảo per-key custom).
        /// Shop tự tick khi tạo artisan keycap, luật switch đặc biệt, v.v.
        /// Mặc định false = chỉ dùng trong flow builder chính.
        public bool IsAddonEligible { get; set; } = false;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public string? Description { get; set; }
        public string? Specifications { get; set; } // JSON string
        public int? RecipeSwitchCount { get; set; }
        public int? RecipeStabilizerCount { get; set; }
        public IFormFile? ThumbnailImage { get; set; }
        public IFormFile? LayerImage { get; set; }
    }
}
