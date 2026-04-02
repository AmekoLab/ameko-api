using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Shop
{
    public class QualityScoreSnapshotDto
    {
        public Guid ShopId { get; set; }
        public DateTime CapturedAt { get; set; }

        public double IssueRate { get; set; }
        public double AvgResponseHours { get; set; }
        public double RefundRate { get; set; }
        public double RepurchaseRate { get; set; }
        public double PositiveFeedbackRate { get; set; }

        public int TotalScore { get; set; }
        public string Badge { get; set; } = string.Empty;
    }
}
