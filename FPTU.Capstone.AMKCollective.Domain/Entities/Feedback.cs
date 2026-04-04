using System;
using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class Feedback : BaseEntity
    {
        public Guid OrderId { get; set; }
        public Guid FromUserId { get; set; }
        public Guid ShopId { get; set; }
        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(2000)]
        public string? Comment { get; set; }

        public int EditCount { get; set; } = 0;

        [MaxLength(2000)]
        public string? ShopReply { get; set; }
        public DateTime? ShopRepliedAt { get; set; }
        public string? Location { get; set; }

        // Navigation Properties
        public virtual Order Order { get; set; } = null!;
        public virtual User FromUser { get; set; } = null!;
        public virtual ShopProfile Shop { get; set; } = null!;
        public virtual ICollection<FeedbackImage> Images { get; set; } = new List<FeedbackImage>();
    }
}
