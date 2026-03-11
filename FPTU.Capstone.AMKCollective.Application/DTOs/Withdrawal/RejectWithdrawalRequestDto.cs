using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Withdrawal
{
    public class RejectWithdrawalRequestDto
    {
        [Required(ErrorMessage = "A reason must be provided when rejecting a withdrawal request.")]
        [MaxLength(1000)]
        public string AdminMessage { get; set; } = null!;
    }
}
