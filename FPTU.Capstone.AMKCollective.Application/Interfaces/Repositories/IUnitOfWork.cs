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
        IWalletRepository Wallets { get; }
        IOrderIssueRepository OrderIssues { get; }
        IOrderIssueLogRepository OrderIssueLogs { get; }
        Task CommitAsync();
        void Rollback();
    }
}
