namespace FPTU.Capstone.AMKCollective.Application.DTOs.Settings
{
    public class SecuritySettings
    {
        public int RefreshTokenLimit { get; set; } = 5;
        //The duration (in minutes) for which a verification code is valid before it expires.
        public int VerificationCodeExpiryMinutes { get; set; } = 15;
        //The duration (in hours) after which unverified accounts will be automatically deleted.
        public int UnverifiedAccountDeletionHours { get; set; } = 24;
    }
}
