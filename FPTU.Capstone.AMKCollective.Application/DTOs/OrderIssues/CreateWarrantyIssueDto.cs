using FPTU.Capstone.AMKCollective.Domain.Enums;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues
{
    public class CreateWarrantyIssueDto
    {
        public CreateWarrantyIssueDto()
        {
            Type = OrderIssueType.WarrantyClaim;
        }

        /// <example>["00000000-0000-0000-0000-000000000000"]</example>
        public List<Guid>? OrderItemIds { get; set; } // Optional: specific items. If null/empty, entire order.

        /// <summary>
        /// 0 = CancelRequest (Immediate Refund), 
        /// 1 = ReturnRequest (Return required if delivered), 
        /// 2 = WarrantyClaim (Immediate Refund)
        /// </summary>
        public OrderIssueType Type { get; set; }

        public string? Reason { get; set; }
        public string? Description { get; set; }

        /// <example>https://example.com/image.jpg</example>
        public string? EvidenceUrl { get; set; }
    }
}
