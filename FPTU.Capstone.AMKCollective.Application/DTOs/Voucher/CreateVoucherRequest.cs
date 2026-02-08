using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Voucher
{
    public class CreateVoucherRequest
    {
        [Required]
        [MaxLength(50)]
        [RegularExpression(@"^[a-zA-Z0-9_]*$", ErrorMessage = "The voucher code may only contain letters, numbers, and underscores.")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        public VoucherType Type { get; set; } // Promotion, Negotiation...

        [Required]
        public DiscountType DiscountType { get; set; } // Percentage, FixedAmount

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Invalid value")]
        public decimal Value { get; set; }

        public decimal? MaxDiscountAmount { get; set; } // Nullable nếu là FixedAmount

        [Range(0, double.MaxValue)]
        public decimal MinOrderValue { get; set; } = 0;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Range(1, int.MaxValue)]
        public int UsageLimit { get; set; } = 1;

        public Guid? TargetUserId { get; set; } // Dùng cho Negotiation
    }
}
