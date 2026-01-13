using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class Follow
    {
        public Guid Id { get; set; }
        public Guid FollowerId { get; set; }
        public Guid FollowedId { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation Properties
        public virtual User Follower { get; set; } = null!;
        public virtual User Followed { get; set; } = null!;

        public Follow()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }
    }
}
