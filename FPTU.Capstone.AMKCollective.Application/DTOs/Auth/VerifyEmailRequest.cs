using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Auth
{
    public class VerifyEmailRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        public string Code { get; set; } = null!;
    }
}
