using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class RefreshToken : BaseEntity
    {
        public Guid UserId { get; set; }
        public string TokenHash { get; set; } = string.Empty;
        public string TokenSalt { get; set; } = string.Empty;

        // Navigation Properties
        public virtual User User { get; set; } = null!;
    }
}
