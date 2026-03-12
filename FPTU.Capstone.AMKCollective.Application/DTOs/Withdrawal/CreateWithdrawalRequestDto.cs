using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Withdrawal
{
    public class CreateWithdrawalRequestDto
    {
        [Required]
        [Range(50000, 1000000000, ErrorMessage = "Withdrawal amount must be between 50,000 and 1,000,000,000.")]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(255)]
        public string BankName { get; set; } = null!;

        [Required]
        [MaxLength(255)]
        public string BankAccountNumber { get; set; } = null!;

        [Required]
        [MaxLength(255)]
        public string BankAccountName { get; set; } = null!;
    }
}
