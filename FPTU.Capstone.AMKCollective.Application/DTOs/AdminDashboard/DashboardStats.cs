using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.AdminDashboard
{
    public class OrderDashboardStats
    {
        public int TotalOrders { get; set; }
        public int CompletedOrders { get; set; }
        public int CancelledOrders { get; set; }
        public int RefundedOrders { get; set; }
        public decimal GrossMerchandiseValue { get; set; }
        public decimal NetRevenue { get; set; }
        public int ActiveBuyers { get; set; }
    }

    public class PaymentDashboardStats
    {
        public int TotalPayments { get; set; }
        public int SuccessfulPayments { get; set; }
        public int FailedPayments { get; set; }
        public int RefundedPayments { get; set; }
        public decimal SuccessfulVolume { get; set; }
        public List<PaymentMethodMetric> MethodMetrics { get; set; } = new();
        public List<PaymentTypeMetric> TypeMetrics { get; set; } = new();
    }

    public class IssueDashboardStats
    {
        public int TotalIssues { get; set; }
        public int OpenIssues { get; set; }
        public int CancelRequests { get; set; }
        public int RefundRequests { get; set; }
        public int DisputeRequests { get; set; }
    }
}
