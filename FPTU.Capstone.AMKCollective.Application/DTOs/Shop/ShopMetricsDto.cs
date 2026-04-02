using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Shop
{
    public class ShopMetricsDto
    {
        public int TotalOrders { get; set; }
        public double IssueRate { get; set; } // Tỷ lệ lỗi (%)
        public double AvgResponseHours { get; set; } // Tốc độ phản hồi trung bình (Giờ)
        public double RefundRate { get; set; } // Tỷ lệ hoàn tiền (%)
        public double RepurchaseRate { get; set; } // Tỷ lệ mua lại (%)
        public double PositiveFeedbackRate { get; set; } // Tỷ lệ feedback tốt (%)
        public double AutoCancelRate { get; set; }
        public int FeedbackCount { get; set; }
    }
}
