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

        // Navigation Properties
        public virtual CommissionRequest CommissionRequest { get; set; } = null!;
        public virtual ShopProfile Shop { get; set; } = null!;
    }
}
