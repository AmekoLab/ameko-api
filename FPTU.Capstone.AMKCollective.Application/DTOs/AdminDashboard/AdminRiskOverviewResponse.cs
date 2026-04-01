namespace FPTU.Capstone.AMKCollective.Application.DTOs.AdminDashboard
{
    public class AdminRiskOverviewResponse
    {
        public DateTime FromUtc { get; set; }
        public DateTime ToUtc { get; set; }

        public int TotalOrders { get; set; }
        public int TotalIssues { get; set; }
        public int OpenIssues { get; set; }

        public int CancelRequests { get; set; }
        public int RefundRequests { get; set; }
        public int DisputeRequests { get; set; }

        public int CancelledOrders { get; set; }
        public int RefundedOrders { get; set; }

        public decimal CancelRate { get; set; }
        public decimal RefundRate { get; set; }
        public decimal IssueRate { get; set; }
    }
}
