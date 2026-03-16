using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Voucher
{
    public class UpdateVoucherRequest
    {
        [MaxLength(255)]
        public string? Name { get; set; }

        public string? Description { get; set; }

        public DateTime? EndDate { get; set; } 

        [Range(0, int.MaxValue)]
        public int? UsageLimit { get; set; }
        public int? MaxUsesPerUser { get; set; }
        public VoucherStatus? Status { get; set; } // Active/Disabled
    }
}
