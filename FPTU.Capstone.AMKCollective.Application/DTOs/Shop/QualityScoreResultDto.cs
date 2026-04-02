using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Shop
{
    public class QualityScoreResultDto
    {
        public ShopMetricsDto RawMetrics { get; set; } = null!;

        // Điểm thành phần đã chuẩn hóa (Thang 100)
        public double IssueScore { get; set; }
        public double ResponseScore { get; set; }
        public double RefundScore { get; set; }
        public double RepurchaseScore { get; set; }
        public double FeedbackScore { get; set; }

        // Điểm tổng và Hạng
        public int TotalScore { get; set; }
        public ShopBadge Badge { get; set; }
    }
}
