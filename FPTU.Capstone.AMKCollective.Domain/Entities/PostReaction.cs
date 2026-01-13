using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class PostReaction
    {
        public Guid Id { get; set; }
        public Guid PostId { get; set; }
        public Guid UserId { get; set; }
        public string Type { get; set; } = string.Empty;

        // Navigation Properties
        public virtual CommunityPost Post { get; set; } = null!;
        public virtual User User { get; set; } = null!;

        public PostReaction()
        {
            Id = Guid.NewGuid();
        }
    }
}
