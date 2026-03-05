using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues
{
    public class UpdateWarrantyIssueDto
    {
        /// <summary>0 = CancelRequest, 1 = ReturnRequest, 2 = WarrantyClaim</summary>
        public OrderIssueType? Type { get; set; }
        public string? Reason { get; set; }
        public string? Description { get; set; }
        public string? EvidenceUrl { get; set; }
    }
}
