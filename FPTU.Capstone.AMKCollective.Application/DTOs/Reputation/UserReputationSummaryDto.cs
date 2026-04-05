using System;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Reputation
{
    public class UserReputationSummaryDto
    {
        public Guid UserId { get; set; }
        public int CurrentScore { get; set; }
        public int MonthlyAutoCancels { get; set; }
        public int TotalAutoCancels { get; set; }
        public int SlowResponseViolationCount { get; set; }
        public int ConsecutiveSuccesses { get; set; }
        public CustomerReputationGate Gate { get; set; } = new CustomerReputationGate();
    }
}
