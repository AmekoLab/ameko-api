using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Wallet
{
    public class WithdrawalActionRequest
    {
        [Required]
        //public bool IsApproved { get; set; } // true = Approve, false = Reject

        public string? Reason { get; set; } // Lý do từ chối (bắt buộc nếu Reject)

        // Có thể thêm bằng chứng chuyển khoản nếu cần (Link ảnh)
        public string? EvidenceImageUrl { get; set; }
    }
}
