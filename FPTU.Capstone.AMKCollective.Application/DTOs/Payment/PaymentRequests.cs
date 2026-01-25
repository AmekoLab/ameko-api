using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Payment
{
    /// <summary>
    /// Request tạo checkout session Stripe
    /// </summary>
    public class CreateCheckoutSessionRequest
    {
        [Required]
        public Guid OrderGroupId { get; set; }

        [Required]
        public string SuccessUrl { get; set; } = string.Empty;

        [Required]
        public string CancelUrl { get; set; } = string.Empty;
    }
}
