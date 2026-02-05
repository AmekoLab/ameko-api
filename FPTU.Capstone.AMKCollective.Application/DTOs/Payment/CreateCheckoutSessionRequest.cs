using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Payment
{
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
