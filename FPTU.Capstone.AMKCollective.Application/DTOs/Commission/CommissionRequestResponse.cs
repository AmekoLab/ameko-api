using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Commission
{
    public class CommissionRequestResponse
    {
        public Guid CommissionRequestId { get; set; }
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;

        public Guid? TargetedShopId { get; set; }
        public string? TargetedShopName { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ReferenceImages { get; set; }
        public decimal? MinBudget { get; set; }
        public decimal? MaxBudget { get; set; }

        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int Quantity { get; set; }

        public int ShopResponseWindowHours { get; set; }
        public int CustomerResponseWindowHours { get; set; }
        public DateTime? ShopResponseDeadlineAt { get; set; }
        public int ReminderCount { get; set; }
        public DateTime? LastReminderAt { get; set; }
        public bool? HasMyPendingQuote { get; set; }


        // Danh sách các báo giá của các Shop dành cho yêu cầu này
        public List<CommissionQuoteResponse> Quotes { get; set; } = new List<CommissionQuoteResponse>();
    }
}
