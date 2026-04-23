using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues
{
    public class OrderIssueResponse
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public decimal OrderTotalAmount { get; set; }
        public decimal CancelledItemsAmount { get; set; }
        public int CancelledItemCount { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string ShopName { get; set; } = string.Empty;
        public OrderIssueType Type { get; set; }
        public OrderIssueStatus Status { get; set; }
        public string Reason { get; set; }
        public string? Description { get; set; }
        public string? ShopResponse { get; set; }
        public string? AIAnalysisResult { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
