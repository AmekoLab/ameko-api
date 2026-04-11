using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Part
{
    public class PartResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string PartType { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public StockStatus Status { get; set; } // Added field
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string? DefaultLayerImageUrl { get; set; }
        public string? Description { get; set; }
        public string? Specifications { get; set; } // JSON
        public int RecipeSwitchCount { get; set; }
        public int RecipeStabilizerCount { get; set; }
        public Guid ShopId { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public bool IsAddonEligible { get; set; }
    }
}
