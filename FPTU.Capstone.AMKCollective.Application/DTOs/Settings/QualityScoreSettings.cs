using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Settings
{
    public class QualityScoreSettings
    {
        public int TimeWindowDays { get; set; }
        public int BatchSize { get; set; }
        public int MinimumOrdersRequired { get; set; }
        public int DefaultScore { get; set; }
        public double MaxIssueRateThreshold { get; set; }
        public double IdealResponseHours { get; set; }
        public double MaxResponseHoursThreshold { get; set; }
        public double MaxRefundRateThreshold { get; set; }
        public double IdealRepurchaseRateThreshold { get; set; }
        public ScoreWeights Weights { get; set; } = new ScoreWeights();
        public BadgeThresholds BadgeThresholds { get; set; } = new BadgeThresholds();
    }
}
