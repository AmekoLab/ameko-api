using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Payment
{
    public class PaymentFilterRequest
    {
        public Guid? UserId { get; set; } // Admin dùng để lọc theo User cụ thể
        public PaymentType? Type { get; set; }
        public PaymentStatus? Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        // Phân trang
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        // Sắp xếp
        public string SortBy { get; set; } = "CreatedAt";
        public bool IsAscending { get; set; } = false;
    }
}
