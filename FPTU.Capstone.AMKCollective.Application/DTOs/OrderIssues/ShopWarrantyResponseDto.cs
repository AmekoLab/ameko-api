using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues
{
    public class ShopWarrantyResponseDto
    {
        [Required]
        public Guid IssueId { get; set; }

        [Required]
        public bool Approve { get; set; }

        public string? ShopResponse { get; set; }
    }
}
