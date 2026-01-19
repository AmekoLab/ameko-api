using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Auth
{
    public class ChangePasswordRequest
    {
        [Required]
        public string OldPassword { get; set; } = null!;
        [Required]
        public string NewPassword { get; set; } = null!;
        [Required]
        [Compare("NewPassword")]
        public string ConfirmNewPassword { get; set; } = null!;
    }
}
