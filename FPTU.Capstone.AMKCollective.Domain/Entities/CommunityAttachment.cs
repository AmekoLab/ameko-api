using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class CommunityAttachment : BaseEntity
    {
        public Guid PostId { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public string? FileType { get; set; }

        // Navigation Properties
        public virtual CommunityPost Post { get; set; } = null!;
    }
}
