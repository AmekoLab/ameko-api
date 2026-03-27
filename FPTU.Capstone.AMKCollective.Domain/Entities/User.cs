using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Reflection;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class User : BaseEntity
    {
        // Role
        public Guid RoleId { get; set; }

        // Required information
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string HashedPassword { get; set; } = null!;

        // Personal information
        public Gender? Gender { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Image { get; set; }

        // Artisan information
        public string? StoreAddress { get; set; }
        public string? Banner { get; set; }
        public double? SuccessDeliveryRate { get; set; }
        public string? StoreDescription { get; set; }

        // Status
        public bool EmailConfirmed { get; set; } = false;
        public bool PhoneNumberConfirmed { get; set; } = false;
        public AccountStatus Status { get; set; } = AccountStatus.PendingVerification;

        // Reputation Point 
        public int YMonthlyAutoCancels { get; set; }  // Số lần hủy tự động trong tháng
        public int TotalAutoCancels { get; set; }  // Tổng số lần hủy tự động
        public int ConsecutiveSuccesses { get; set; } // Số đơn hàng thành công s
                                                      // System
        public string? VerificationCode { get; set; }
        public DateTime? VerificationCodeExpiryTime { get; set; }
        public string? ResetPasswordToken { get; set; }

        // Navigation Properties
        public virtual Role Role { get; set; } = null!;
        public virtual ICollection<Order> CustomerOrders { get; set; } = new List<Order>();
        public virtual ICollection<Voucher> CreatedVouchers { get; set; } = new List<Voucher>();
        public virtual ICollection<VoucherUsageLog> VoucherUsageLogs { get; set; } = new List<VoucherUsageLog>();
        public virtual ICollection<Follow> Followers { get; set; } = new List<Follow>();
        public virtual ICollection<Follow> Following { get; set; } = new List<Follow>();
        public virtual ICollection<Feedback> SentFeedbacks { get; set; } = new List<Feedback>();
        public virtual ICollection<Feedback> ReceivedFeedbacks { get; set; } = new List<Feedback>();
        public virtual ICollection<UserConversation> UserConversations { get; set; } = new List<UserConversation>();
        public virtual ICollection<MessageRecipient> MessageRecipients { get; set; } = new List<MessageRecipient>();
        public virtual ICollection<Message> MessagesCreated { get; set; } = new List<Message>();
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public virtual ShopProfile? ShopProfile { get; set; }
        public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public virtual ICollection<PostReaction> PostReactions { get; set; } = new List<PostReaction>();
        public virtual ICollection<PostComment> PostComments { get; set; } = new List<PostComment>();
        public virtual ICollection<CommunityPost> CommunityPosts { get; set; } = new List<CommunityPost>();
        
        // Payment & Wallet
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public virtual Wallet? Wallet { get; set; }
        
        // Order Issues
        public virtual ICollection<OrderIssue> OrderIssues { get; set; } = new List<OrderIssue>();
        public virtual ICollection<OrderIssueLog> OrderIssueActions { get; set; } = new List<OrderIssueLog>();

        // CommissionRequest
        public virtual ICollection<CommissionRequest> CommissionRequests { get; set; } = new List<CommissionRequest>();

        // WithdrawalRequests
        public virtual ICollection<WithdrawalRequest> WithdrawalRequests { get; set; } = new List<WithdrawalRequest>();
        public virtual ICollection<WithdrawalRequest> ApprovedWithdrawalRequests { get; set; } = new List<WithdrawalRequest>();
    }
}
