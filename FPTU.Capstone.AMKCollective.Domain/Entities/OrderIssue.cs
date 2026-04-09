using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class OrderIssue :BaseEntity
    {
        public Guid OrderId { get; set; }
        public Guid UserId { get; set; } // Người tạo yêu cầu (Customer)

        public OrderIssueType Type { get; set; } // Cancel, Return, Warranty
        public OrderIssueStatus Status { get; set; } // Pending, InProgress...

        public string? Reason { get; set; } // Lý do chọn từ Dropdown
        public string? Description { get; set; } // Mô tả chi tiết
        public string? EvidenceUrl { get; set; } // Link ảnh/video bằng chứng (có thể ngăn cách bằng dấu phẩy)

        // Các field hỗ trợ xử lý
        public bool IsSystemValid { get; set; } = false; // System check logic (1.2)
        public string? ShopResponse { get; set; } // Shop phản hồi lý do từ chối
        public string? AdminNote { get; set; } // Ghi chú của Admin
        public string? AIAnalysisResult { get; set; } // Kết quả phân tích của AI

        // Navigation Properties
        public virtual Order? Order { get; set; }
        public virtual User? User { get; set; }

        // Relationship 1-n với Log
        public virtual ICollection<OrderIssueLog> Logs { get; set; } = new List<OrderIssueLog>();
    }
}

