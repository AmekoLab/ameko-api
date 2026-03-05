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
        IOrderVoucherRepository OrderVouchers { get; }
        ICommissionRequestRepository CommissionRequests { get; }
        ICommissionQuoteRepository CommissionQuotes { get; }
        Task CommitAsync();
        void Rollback();
        /// <summary>
        /// Detach all tracked entities from the change tracker.
        /// Use when you need a clean tracking state (e.g., after read-only validation queries).
        /// </summary>
        void ClearChangeTracker();
    }
}
