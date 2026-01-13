using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class PostComment
    {
        public Guid Id { get; set; }
        public Guid PostId { get; set; }
        public Guid UserId { get; set; }

        // Navigation Properties
        public virtual CommunityPost Post { get; set; } = null!;
        public virtual User User { get; set; } = null!;

        public PostComment()
        {
            Id = Guid.NewGuid();
        }
    }
}
