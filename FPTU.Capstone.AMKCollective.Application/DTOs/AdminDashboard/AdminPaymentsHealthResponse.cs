using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.AdminDashboard
{
    public class AdminPaymentsHealthResponse
    {
        public DateTime FromUtc { get; set; }
        public DateTime ToUtc { get; set; }

        public int TotalPayments { get; set; }
        public int SuccessfulPayments { get; set; }
        public int FailedPayments { get; set; }
        public int RefundedPayments { get; set; }

        public decimal SuccessfulPaymentVolume { get; set; }
        public decimal PaymentSuccessRate { get; set; }

        public List<PaymentMethodMetric> MethodMetrics { get; set; } = new();
        public List<PaymentTypeMetric> TypeMetrics { get; set; } = new();
    }

    public class PaymentMethodMetric
    {
        public PaymentMethod Method { get; set; }
        public int Total { get; set; }
        public int Successful { get; set; }
        public int Failed { get; set; }
    }

    public class PaymentTypeMetric
    {
        public PaymentType Type { get; set; }
        public int Total { get; set; }
        public decimal Amount { get; set; }
    }
}
