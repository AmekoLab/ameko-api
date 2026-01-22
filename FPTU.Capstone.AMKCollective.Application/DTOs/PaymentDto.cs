using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs
{
    public class CreateCheckoutSessionRequest
    {
        [Required]
        public Guid OrderGroupId { get; set; }

        [Required]
        public string SuccessUrl { get; set; } = string.Empty; // URL frontend trang "Cảm ơn"

        [Required]
        public string CancelUrl { get; set; } = string.Empty;  // URL frontend trang "Hủy/Lỗi"
    }

    public class CheckoutSessionResponse
    {
        public string SessionId { get; set; } = string.Empty; 
        public string PaymentUrl { get; set; } = string.Empty; // Link redirect qua Stripe
    }

    // DTO này sẽ được lồng vào trong OrderGroupDto để user xem lại
    public class PaymentDto
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "vnd";
        public string Status { get; set; } = string.Empty; // Success/Failed/Pending
        public string PaymentMethod { get; set; } = "CreditCard";
        public DateTime CreatedAt { get; set; }
    }
}