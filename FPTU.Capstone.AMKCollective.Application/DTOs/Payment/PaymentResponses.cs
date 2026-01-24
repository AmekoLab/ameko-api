namespace FPTU.Capstone.AMKCollective.Application.DTOs.Payment
{
    /// <summary>
    /// Response checkout session
    /// </summary>
    public class CheckoutSessionResponse
    {
        public string SessionId { get; set; } = string.Empty;
        public string PaymentUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO thông tin thanh toán
    /// </summary>
    public class PaymentDto
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "vnd";
        public string Status { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = "CreditCard";
        public DateTime CreatedAt { get; set; }
    }
}
