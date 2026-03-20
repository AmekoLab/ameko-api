using System;
using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    /// <summary>
    /// Post reaction entity with int PK for better performance (high-volume inserts).
    /// </summary>
    public class PostReaction : BaseEntityInt
    {
        public int PostId { get; set; }  // FK to CommunityPost (int)
        public Guid UserId { get; set; } // FK to User (Guid)
        public ReactionType Type { get; set; }

        // Navigation Properties
        public virtual CommunityPost Post { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}
