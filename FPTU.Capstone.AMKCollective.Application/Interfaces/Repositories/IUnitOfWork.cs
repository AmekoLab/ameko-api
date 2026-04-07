using System.Threading.Tasks;
namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IUnitOfWork
    {
        IUserRepository Users { get; }
        ICategoryRepository Categories { get; }
        IModelRepository Models { get; }
        IKitDesignOptionRepository KitDesignOptions { get; }
        IBuilderSessionRepository BuilderSessions { get; }
        IOrderRepository Orders { get; }
        IOrderGroupRepository OrderGroups { get; }
        IShopRepository Shops { get; }
        IPaymentRepository Payments {  get; }
        IFollowRepository Follows { get; }
        IAssembledProductRepository AssembledProducts { get; }
        IWalletRepository Wallets { get; }
        IOrderIssueRepository OrderIssues { get; }
        IOrderIssueLogRepository OrderIssueLogs { get; }
        IVoucherRepository Vouchers { get; }
        INotificationRepository Notifications { get; }
        ICommunityPostRepository CommunityPosts { get; }
        //IOrderVoucherRepository OrderVouchers { get; }
        IVoucherUsageLogRepository VoucherUsageLogs { get; }
        ICommissionRequestRepository CommissionRequests { get; }
        ICommissionQuoteRepository CommissionQuotes { get; }
        IAssemblyProgressLogRepository AssemblyProgressLogs { get; }
        IAssemblyStepTemplateRepository AssemblyStepTemplates { get; }
        Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action);
        IWithdrawalRequestRepository WithdrawalRequests { get; }
        ITransactionRepository Transactions{ get; }
        IPostReactionRepository PostReactions { get; }
        IPostCommentRepository PostComments { get; }
        IConversationRepository Conversations { get; }
        IMessageRepository Messages { get; }
        ICartRepository Carts { get; }
        ICartItemRepository CartItems { get; }
        IFeedbackRepository Feedbacks { get; }
        IAssembledProductFeedbackRepository AssembledProductFeedbacks { get; }
        IShopAnalyticsRepository ShopAnalytics { get; }
        IQualityScoreSnapshotRepository QualityScoreSnapshots { get; }
        IReputationLogRepository ReputationLogs { get; }
        Task CommitAsync();
        void Rollback();
        /// <summary>
        /// Detach all tracked entities from the change tracker.
        /// Use when you need a clean tracking state (e.g., after read-only validation queries).
        /// </summary>
        void ClearChangeTracker();

        /// <summary>
        /// Execute a block of code within a database transaction and an execution strategy.
        /// </summary>
        Task ExecuteTransactionAsync(System.Func<Task> action);
    }
}
