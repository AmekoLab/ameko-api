using FPTU.Capstone.AMKCollective.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Payment
{
    public class DepositRequest
    {
        [Required]
        [Range(10000, double.MaxValue, ErrorMessage = "Minimum deposit amount is 10,000 VND")]
        public decimal Amount { get; set; }

        /// <summary>Phương thức thanh toán: CreditCard (Stripe) hoặc VnPay. Mặc định là CreditCard.</summary>
        public PaymentMethod Method { get; set; } = PaymentMethod.CreditCard;

        public string? SuccessUrl { get; set; }
        public string? CancelUrl { get; set; }
    }
}
