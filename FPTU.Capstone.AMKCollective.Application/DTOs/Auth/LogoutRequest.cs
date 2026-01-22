using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Auth
{
    public class LogoutRequest
    {
        [Required]
        public string RefreshToken { get; set; } = null!;
    }
}
