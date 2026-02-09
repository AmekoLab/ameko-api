using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    public class SessionInfoResponse
    {
        public Guid Id { get; set; }
        public Dictionary<string, SelectedPartResponse> Selection { get; set; } = new();
        public decimal TotalPrice { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsComplete { get; set; }

        /// <summary>
        /// Ảnh tích lũy hiện tại - FE chỉ cần hiển thị trường này vào khung Preview.
        /// Đây là ảnh của linh kiện cuối cùng được chọn (đã bao gồm nền tất cả bước trước).
        /// </summary>
        public string? CurrentPreviewImage { get; set; }
    }
}
