using FPTU.Capstone.AMKCollective.Application.Interfaces;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private IUserRepository? _users;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        // Repository properties - The repositories will be implemented similarly do not forget to create them please!!!
        public IUserRepository Users => _users ??= new UserRepository(_context);

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
