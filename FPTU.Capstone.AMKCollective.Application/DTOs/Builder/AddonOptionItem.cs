using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    public class AddonOptionItem
    {
        public Guid PartId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? PartType { get; set; }
        public int StockQuantity { get; set; }

        /// <summary>
        /// Chỉ có giá trị với switch (từ KitDesignOption).
        /// Null với keycap artisan (từ Model trực tiếp).
        /// </summary>
        public Guid? KitDesignOptionId { get; set; }

        /// <summary>Tag tương thích nhánh, VD: "case-den"</summary>
        public string? Tags { get; set; }
    }
}
