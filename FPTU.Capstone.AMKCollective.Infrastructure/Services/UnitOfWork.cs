using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;

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

        public async Task CommitAsync()
        {
            await _context.SaveChangesAsync();
        }

        public void Rollback()
        {
            // EF Core không hỗ trợ rollback ngoài transaction scope, có thể implement nếu dùng transaction
        }
    }
}
