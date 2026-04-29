namespace FPTU.Capstone.AMKCollective.Application.DTOs.Payment
{
    public class VerifySessionResponse
    {
        public string SessionId { get; set; } = string.Empty;
        /// <summary>"paid" | "unpaid" | "no_payment_required"</summary>
        public string PaymentStatus { get; set; } = string.Empty;
        public bool IsPaid { get; set; }
    }
}
