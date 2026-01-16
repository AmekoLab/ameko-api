using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class CommunityPost : BaseEntity
    {
        public Guid UserId { get; set; }
        public string Title { get; set; } = string.Empty;

        // Navigation Properties
        public virtual User User { get; set; } = null!;
        public virtual ICollection<PostReaction> PostReactions { get; set; } = new List<PostReaction>();
        public virtual ICollection<PostComment> PostComments { get; set; } = new List<PostComment>();
        public virtual ICollection<CommunityAttachment> Attachments { get; set; } = new List<CommunityAttachment>();
    }
}
