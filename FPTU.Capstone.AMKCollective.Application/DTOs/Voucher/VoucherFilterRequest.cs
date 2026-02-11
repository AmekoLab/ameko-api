using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Voucher
{
    public class VoucherFilterRequest
    {
        public string? SearchCode { get; set; } // Tìm theo mã hoặc tên
        public VoucherStatus? Status { get; set; } // Lọc theo trạng thái (Active, Inactive...)
        public bool? IsExpired { get; set; } // true: Lấy voucher đã hết hạn
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        // Phân trang 
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
