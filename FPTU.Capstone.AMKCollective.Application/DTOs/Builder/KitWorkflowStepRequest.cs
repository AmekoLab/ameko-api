using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    public class KitWorkflowStep
    {
        public string Step { get; set; } = string.Empty; // Tên bước hệ thống (vd: case, plate)
        public string Title { get; set; } = string.Empty; // Tên hiển thị (vd: Chọn Vỏ Case)
        public string RequiredTag { get; set; } = string.Empty; // Tag bắt buộc (nếu có)
        public int Quantity { get; set; } = 1; // Số lượng cần cho bước này
    }
}
