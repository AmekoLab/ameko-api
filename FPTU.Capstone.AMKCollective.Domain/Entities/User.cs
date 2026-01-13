using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Gender { get; set; }

        // Navigation Properties
        public virtual ICollection<Order> CustomerOrders { get; set; } = new List<Order>();
        public virtual ICollection<Voucher> CreatedVouchers { get; set; } = new List<Voucher>();
        public virtual ICollection<VoucherUsageLog> VoucherUsageLogs { get; set; } = new List<VoucherUsageLog>();
        public virtual ICollection<Follow> Followers { get; set; } = new List<Follow>();
        public virtual ICollection<Follow> Following { get; set; } = new List<Follow>();
        public virtual ICollection<Feedback> SentFeedbacks { get; set; } = new List<Feedback>();
        public virtual ICollection<Feedback> ReceivedFeedbacks { get; set; } = new List<Feedback>();
        public virtual ICollection<Conversation> ConversationsAsUserOne { get; set; } = new List<Conversation>();
        public virtual ICollection<Conversation> ConversationsAsUserTwo { get; set; } = new List<Conversation>();
        public virtual ICollection<Message> SentMessages { get; set; } = new List<Message>();
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public virtual ShopProfile? ShopProfile { get; set; }
        public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public virtual ICollection<PostReaction> PostReactions { get; set; } = new List<PostReaction>();
        public virtual ICollection<PostComment> PostComments { get; set; } = new List<PostComment>();
        public virtual ICollection<CommunityPost> CommunityPosts { get; set; } = new List<CommunityPost>();

        public User()
        {
            Id = Guid.NewGuid();
        }
    }
}
