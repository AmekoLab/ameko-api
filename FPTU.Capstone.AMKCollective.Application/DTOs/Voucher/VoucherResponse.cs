using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Voucher
{
    public class VoucherResponse
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        // Trả về string của Enum để FE dễ hiển thị
        public string Type { get; set; } = string.Empty;
        public string DiscountType { get; set; } = string.Empty;

        public decimal Value { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public decimal MinOrderValue { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public int UsageLimit { get; set; }
        public int UsedCount { get; set; }

        public string Status { get; set; } = string.Empty;

        public Guid CreatorId { get; set; }
        public string? CreatorName { get; set; } // Tên Shop tạo

        public Guid? TargetUserId { get; set; } // Nếu có, chỉ user này thấy
        public int? MaxUsesPerUser { get; set; }
        public string Scope { get; set; } = string.Empty;
        // Stacking info
        public bool IsStackable { get; set; }
        public string StackingPolicy { get; set; } = string.Empty;
    }
}
