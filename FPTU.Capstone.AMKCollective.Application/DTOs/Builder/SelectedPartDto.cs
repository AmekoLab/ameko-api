using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    // DTO này dùng để Serialize/Deserialize chuỗi JSON lưu trong Database
    public class SelectedPartDto
    {
        public Guid PartId { get; set; }
        public string PartName { get; set; }
        public string Step { get; set; } // Ví dụ: "case", "plate", "led"
        public decimal Price { get; set; } // Giá tại thời điểm chọn
        public int Quantity { get; set; }
        public string? LayerImageUrl { get; set; } // Link ảnh tích lũy tại bước này

        // Helper tính tổng tiền của riêng món này
        public decimal TotalPrice => Price * Quantity;
    }
}
