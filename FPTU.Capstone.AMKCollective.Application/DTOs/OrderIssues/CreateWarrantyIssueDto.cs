using FPTU.Capstone.AMKCollective.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues
{
    public class CreateWarrantyIssueDto
    {
        [Required]
        public Guid OrderId { get; set; }

        [Required]
        public OrderIssueType Type { get; set; }

        [Required]
        public string Reason { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public string EvidenceUrl { get; set; } = string.Empty;
    }
}
