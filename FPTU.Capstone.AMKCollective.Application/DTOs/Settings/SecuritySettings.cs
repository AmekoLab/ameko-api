namespace FPTU.Capstone.AMKCollective.Application.DTOs.Settings
{
    public class SecuritySettings
    {
        public int RefreshTokenLimit { get; set; } = 5;
        public int VerificationCodeExpiryMinutes { get; set; } = 15;
    }
}
