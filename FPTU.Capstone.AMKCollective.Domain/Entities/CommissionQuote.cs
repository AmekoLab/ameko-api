using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class CommissionQuote :BaseEntity
    {
        public Guid CommissionRequestId { get; set; }
        public Guid ShopId { get; set; }

        // Giá tiền shop nhận thi công
        [Column(TypeName = "decimal(18,2)")]
        public decimal QuotedPrice { get; set; }

        // Thời gian dự kiến hoàn thành (tính bằng ngày)
        public int EstimatedDays { get; set; }

        // Lời nhắn, ghi chú của shop gửi cho khách (vd: "Phím này a phải order taobao 7 ngày nha")
        public string? ShopNotes { get; set; }

        public QuoteStatus Status { get; set; } = QuoteStatus.PendingUserDecision;
        public DateTime ExpiredAt { get; set; }
        public DateTime? CustomerDecisionDeadlineAt { get; set; }
        public int CustomerReminderCount { get; set; } = 0;
        public DateTime? LastCustomerReminderAt { get; set; }

        // Navigation Properties
        public virtual CommissionRequest CommissionRequest { get; set; } = null!;
        public virtual ShopProfile Shop { get; set; } = null!;
    }
}
