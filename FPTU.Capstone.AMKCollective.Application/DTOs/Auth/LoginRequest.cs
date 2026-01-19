using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Auth
{
    public class LoginRequest
    {
        [Required]       
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        public string Password { get; set; } = null!;
    }
}
