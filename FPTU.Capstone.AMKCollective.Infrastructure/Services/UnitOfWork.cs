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
