using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class OrderIssueLog :BaseEntity
    {
        public Guid OrderIssueId { get; set; }

        public Guid ActionById { get; set; } // Id người thực hiện (User, ShopOwner, Admin)
        public RoleType ActionByRole { get; set; } // Role người thực hiện

        public OrderIssueAction Action { get; set; } // Create, ShopReject, AdminDecision...

        public string? Comment { get; set; } // Nội dung chat/lý do
        public string? EvidenceUrl { get; set; } // Bằng chứng bổ sung tại bước này (nếu có)

        // Lưu kết quả phán quyết (chỉ dùng khi Action = AdminDecision)
        public bool? AdminDecision { get; set; } // True = Chấp nhận trả, False = Từ chối

        // Navigation Properties
        public virtual OrderIssue OrderIssue { get; set; }
        public virtual User ActionBy { get; set; }
    }
}
