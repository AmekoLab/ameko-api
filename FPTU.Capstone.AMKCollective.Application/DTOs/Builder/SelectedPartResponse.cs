using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    public class SelectedPartResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string ThumbnailUrl { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;

        // === Nhánh hình ảnh tích lũy ===
        /// <summary>ID của bản ghi KitDesignOption cụ thể đã chọn (để restore chính xác nhánh)</summary>
        public Guid KitDesignOptionId { get; set; }
        /// <summary>Ảnh tích lũy (cumulative) - ảnh bao gồm tất cả linh kiện ở các bước trước</summary>
        public string? LayerImageUrl { get; set; }
        /// <summary>Luật lọc bước tiếp theo, VD: "plate:case-den"</summary>
        public string? NextStepFilterRule { get; set; }
    }
}
