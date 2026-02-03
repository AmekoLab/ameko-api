using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    /// <summary>
    /// Community attachment entity with int PK for better performance (high-volume inserts).
    /// </summary>
    public class CommunityAttachment : BaseEntityInt
    {
        public int PostId { get; set; }  // FK to CommunityPost (int)
        public string FileUrl { get; set; } = string.Empty;
        public string? FileType { get; set; }

        // Navigation Properties
        public virtual CommunityPost Post { get; set; } = null!;
    }
}
