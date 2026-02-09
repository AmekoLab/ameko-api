using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    public class CompatiblePartResponse
    {
        /// <summary>ID của bản ghi KitDesignOption (dùng để phân biệt các nhánh của cùng 1 component)</summary>
        public Guid OptionId { get; set; }
        public Guid PartId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string ThumbnailUrl { get; set; } = string.Empty;
        /// <summary>Ảnh tích lũy - bao gồm nền của tất cả bước trước (theo nhánh)</summary>
        public string LayerImageUrl { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public StockStatus Status { get; set; }

        // === Nhánh hình ảnh tích lũy ===
        /// <summary>Tag nhánh, VD: "case-den" — FE dùng để debug/log nếu cần</summary>
        public string? Tags { get; set; }
        /// <summary>Luật lọc bước tiếp theo, VD: "plate:case-den"</summary>
        public string? NextStepFilterRule { get; set; }
    }
}
