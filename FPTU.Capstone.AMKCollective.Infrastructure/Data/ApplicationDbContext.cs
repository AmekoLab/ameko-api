using Microsoft.EntityFrameworkCore;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // DbSets
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Model> Models { get; set; } = null!;
        public DbSet<ShopProfile> ShopProfiles { get; set; } = null!;
        public DbSet<AssembledProduct> AssembledProducts { get; set; } = null!;
        public DbSet<KitDesignOption> KitDesignOptions { get; set; } = null!;
        public DbSet<BuilderSession> BuilderSessions { get; set; }
        public DbSet<ProductAssembledDetail> ProductAssembledDetails { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<OrderGroup> OrderGroups { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;
        public DbSet<OrderItem> OrderItems { get; set; } = null!;
        public DbSet<OrderItemComponent> OrderItemComponents { get; set; } = null!;
        public DbSet<Voucher> Vouchers { get; set; } = null!;
        public DbSet<VoucherUsageLog> VoucherUsageLogs { get; set; } = null!;
        public DbSet<Follow> Follows { get; set; } = null!;
        public DbSet<Feedback> Feedbacks { get; set; } = null!;
        public DbSet<Conversation> Conversations { get; set; } = null!;
        public DbSet<Message> Messages { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
        public DbSet<CommunityPost> CommunityPosts { get; set; } = null!;
        public DbSet<PostReaction> PostReactions { get; set; } = null!;
        public DbSet<PostComment> PostComments { get; set; } = null!;
        public DbSet<CommunityAttachment> CommunityAttachments { get; set; } = null!;
        public DbSet<Wallet> Wallets { get; set; } = null!;
        public DbSet<Transaction> Transactions { get; set; } = null!;
        public DbSet<OrderIssue> OrderIssues { get; set; } = null!;
        public DbSet<OrderIssueLog> OrderIssueLogs { get; set; } = null!;
        public DbSet<WithdrawalRequest> WithdrawalRequests { get; set; } = null!;
        public DbSet<CommissionRequest> CommissionRequests => Set<CommissionRequest>();
        public DbSet<CommissionQuote> CommissionQuotes => Set<CommissionQuote>();
        public DbSet<AssemblyProgressLog> AssemblyProgressLogs { get; set; }
        public DbSet<AssemblyStepTemplate> AssemblyStepTemplates { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Apply all configurations from assembly (includes UserConfiguration etc.)
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

            modelBuilder.Entity<Follow>().HasIndex(f => f.FollowedId);


            // SEED DATA: SYSTEM BOT ACCOUNT
            var systemBotId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var adminId = Guid.Parse("11111111-1111-1111-1111-111111111111");

            modelBuilder.Entity<User>().HasData(new User
            {
                Id = systemBotId,
                Username = "systembot",
                Email = "noreply@amkcollective.com",
                HashedPassword = "NoPasswordNeededForBot",
                FirstName = "AMK",
                LastName = "System",
                Status = Domain.Enums.AccountStatus.Active, 
                RoleId = adminId, 
                EmailConfirmed = true,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) 
            });
        }
    }
}
