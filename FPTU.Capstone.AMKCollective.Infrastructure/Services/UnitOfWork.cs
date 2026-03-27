using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private IUserRepository? _users;
        private ICategoryRepository? _categories;
        private IModelRepository? _models;
        private IKitDesignOptionRepository? _kitDesignOptions;
        private IBuilderSessionRepository? _builderSessions;
        private IShopRepository? _shops;
        private IFollowRepository? _follows;
        private IAssembledProductRepository? _assembledProducts;
        private IOrderRepository? _order;
        private IOrderGroupRepository? _orderGroups;
        private IPaymentRepository? _payments;
        private IWalletRepository? _wallets;
        private IOrderIssueRepository? _orderIssues;
        private IOrderIssueLogRepository? _orderIssueLogs;
        private IVoucherRepository? _voucher;
        private INotificationRepository? _notifications;
        private ICommunityPostRepository? _communityPosts;
        //private IOrderVoucherRepository? _orderVoucher;
        private IVoucherUsageLogRepository? _voucherUsageLogs;
        private ICommissionRequestRepository? _commissionRequests;
        private ICommissionQuoteRepository? _commissionQuotes;
        private IAssemblyProgressLogRepository? _assemblyProgressLogs;
        private IAssemblyStepTemplateRepository? _assemblyStepTemplates;
        private IDbContextTransaction? _currentTransaction;
        private IWithdrawalRequestRepository? _withdrawalRequests;
        private ITransactionRepository? _transactions;
        private IPostReactionRepository? _postReactions;
        private IPostCommentRepository? _postComments;
        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        // Repository properties
        public IUserRepository Users => _users ??= new UserRepository(_context);
        public ICategoryRepository Categories => _categories ??= new CategoryRepository(_context);
        public IModelRepository Models => _models ??= new ModelRepository(_context);
        public IKitDesignOptionRepository KitDesignOptions => _kitDesignOptions ??= new KitDesignOptionRepository(_context);
        public IBuilderSessionRepository BuilderSessions => _builderSessions ??= new BuilderSessionRepository(_context);
        public IShopRepository Shops => _shops ??= new ShopRepository(_context);
        public IFollowRepository Follows => _follows ??= new FollowRepository(_context);
        public IAssembledProductRepository AssembledProducts => _assembledProducts ??= new AssembledProductRepository(_context);

        public IOrderRepository Orders => _order ??= new OrderRepository(_context);
        public IOrderGroupRepository OrderGroups => _orderGroups ??= new OrderGroupRepository(_context);
        public IPaymentRepository Payments => _payments ??= new PaymentRepository(_context);
        
        public IWalletRepository Wallets => _wallets ??= new WalletRepository(_context);
        public IOrderIssueRepository OrderIssues => _orderIssues ??= new OrderIssueRepository(_context);
        public IOrderIssueLogRepository OrderIssueLogs => _orderIssueLogs ??= new OrderIssueLogRepository(_context);
        public IVoucherRepository Vouchers => _voucher ??= new VoucherRepository(_context);
        public INotificationRepository Notifications => _notifications ??= new NotificationRepository(_context);
        public ICommunityPostRepository CommunityPosts => _communityPosts ??= new CommunityPostRepository(_context);
        //public IOrderVoucherRepository OrderVouchers => _orderVoucher ??= new OrderVoucherRepository(_context);
        public IVoucherUsageLogRepository VoucherUsageLogs => _voucherUsageLogs ??= new VoucherUsageLogRepository(_context);
        public ICommissionRequestRepository CommissionRequests => _commissionRequests ??= new CommissionRequestRepository(_context);
        public ICommissionQuoteRepository CommissionQuotes => _commissionQuotes ??= new CommissionQuoteRepository(_context);
        public IAssemblyProgressLogRepository AssemblyProgressLogs => _assemblyProgressLogs ??= new AssemblyProgressLogRepository(_context);

        public IAssemblyStepTemplateRepository AssemblyStepTemplates => _assemblyStepTemplates ??= new AssemblyStepTemplateRepository(_context);
        public IWithdrawalRequestRepository WithdrawalRequests => _withdrawalRequests ??= new WithdrawalRequestRepository(_context);
        public ITransactionRepository Transactions => _transactions ??= new TransactionRepository(_context);
        public IPostReactionRepository PostReactions => _postReactions ??= new PostReactionRepository(_context);
        public IPostCommentRepository PostComments => _postComments ??= new PostCommentRepository(_context);

        public async Task CommitAsync()
        {
            await _context.SaveChangesAsync();
        }

        public void Rollback()
        {
            // EF Core không hỗ trợ rollback ngoài transaction scope, có thể implement nếu dùng transaction
        }

        public void ClearChangeTracker()
        {
            _context.ChangeTracker.Clear();
        }

        public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action)
        {
            // Tạo ra một Execution Strategy, Retry tự động nếu đứt mạng
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                // Khởi tạo Transaction NẰM TRONG Strategy
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Chạy toàn bộ logic được truyền vào
                    var result = await action();

                    // Nếu không có lỗi gì ném ra, xác nhận Transaction
                    await transaction.CommitAsync();
                    return result;
                }
                catch
                {
                    // Bất cứ lỗi gì xảy ra, lập tức Rollback trả lại kho
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
        public async Task ExecuteTransactionAsync(System.Func<Task> action)
        {
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
                await action();
                // The transaction will be committed in the action loop or here, depending on how it's used.
                // It is safer to commit it here dynamically. But _unitOfWork.CommitAsync is called inside the action.
                // Either way is fine, we just commit the transaction directly here to be consistent.
                await transaction.CommitAsync();
            });
        }
    }
}
