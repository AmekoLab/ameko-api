using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    /// <summary>
    /// Post reaction entity with int PK for better performance (high-volume inserts).
    /// </summary>
    public class PostReaction : BaseEntityInt
    {
        public int PostId { get; set; }  // FK to CommunityPost (int)
        public Guid UserId { get; set; } // FK to User (Guid)
        public string Type { get; set; } = string.Empty;

        // Navigation Properties
        public virtual CommunityPost Post { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}
