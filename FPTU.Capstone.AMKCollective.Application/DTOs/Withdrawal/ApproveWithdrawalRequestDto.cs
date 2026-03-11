using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Withdrawal
{
    public class ApproveWithdrawalRequestDto
    {
        [Required(ErrorMessage = "Evidence URL (such as a banking receipt screenshot) is required to approve the withdrawal.")]
        [Url(ErrorMessage = "Evidence must be a valid URL.")]
        public string EvidenceUrl { get; set; } = null!;

        [MaxLength(1000)]
        public string? AdminMessage { get; set; }
    }
}
