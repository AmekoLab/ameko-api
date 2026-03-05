using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues
{
    public class ReturnWarrantyShipmentDto
    {
        [Required]
        public Guid IssueId { get; set; }

        [Required]
        public string EvidenceUrl { get; set; } = string.Empty;

        public string? Comment { get; set; }
    }
}
