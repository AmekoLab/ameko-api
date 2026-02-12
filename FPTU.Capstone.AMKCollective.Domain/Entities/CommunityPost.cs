using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    /// <summary>
    /// Community post entity with int PK for better performance (high-volume inserts).
    /// </summary>
    public class CommunityPost : BaseEntityInt
    {
        public Guid UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public Guid? AssembledProductId { get; set; }
        // Navigation Properties
        public virtual User User { get; set; } = null!;
        public virtual ICollection<PostReaction> PostReactions { get; set; } = new List<PostReaction>();
        public virtual ICollection<PostComment> PostComments { get; set; } = new List<PostComment>();
        public virtual ICollection<CommunityAttachment> Attachments { get; set; } = new List<CommunityAttachment>();
    }
}
