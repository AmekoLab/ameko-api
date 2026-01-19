using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Auth
{
    public class RefreshTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; } = null!;
    }
}
