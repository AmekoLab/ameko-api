using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class Follow : BaseEntity
    {
        public Guid FollowerId { get; set; }
        public Guid FollowedId { get; set; }

        // Navigation Properties
        public virtual User Follower { get; set; } = null!;
        public virtual User Followed { get; set; } = null!;
    }
}
