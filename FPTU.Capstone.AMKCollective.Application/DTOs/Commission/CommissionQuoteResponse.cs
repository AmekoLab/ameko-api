using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Commission
{
    public class CommissionQuoteResponse
    {
        public Guid CommissionQuoteId { get; set; }
        public Guid CommissionRequestId { get; set; }
        public Guid ShopId { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public string? ShopAvatar { get; set; }

        public decimal QuotedPrice { get; set; }
        public int EstimatedDays { get; set; }
        public string? ShopNotes { get; set; }

        public string Status { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
        public DateTime ExpiredAt { get; set; }
    }
}
