using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Voucher
{
    public class CreateNegotiationRequest
    {
        [Required]
        public Guid TargetUserId { get; set; } 

        [Required]
        [Range(0, double.MaxValue)]
        public decimal DiscountAmount { get; set; } 

        [Required]
        [Range(0, double.MaxValue)]
        public decimal MinOrderValue { get; set; } 
    }
}
