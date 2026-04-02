using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class QualityScoreSnapshot : BaseEntity
    {
        public Guid ShopId { get; set; }
        public DateTime CapturedAt { get; set; } // Ngày chốt số liệu

        // 5 Chỉ số thành phần thô
        public double IssueRate { get; set; } // Tỷ lệ khiếu nại (%)
        public double AvgResponseHours { get; set; } // Tốc độ phản hồi trung bình (Giờ)
        public double RefundRate { get; set; } // Tỷ lệ hoàn tiền (%)
        public double RepurchaseRate { get; set; } // Tỷ lệ khách mua lại (%)
        public double PositiveFeedbackRate { get; set; } // Tỷ lệ đánh giá tốt (%)

        // Kết quả tính toán
        public double AutoCancelRate { get; set; } // Tỷ lệ tự hủy (%)
        public int FeedbackCount { get; set; }
        public int TotalScore { get; set; }
        public ShopBadge Badge { get; set; }

        // Navigation Property
        public virtual ShopProfile Shop { get; set; } = null!;
    }
}
