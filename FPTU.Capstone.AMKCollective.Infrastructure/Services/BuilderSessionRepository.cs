using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;


namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class BuilderSessionRepository : IBuilderSessionRepository
    {
        private readonly ApplicationDbContext _context;

        public BuilderSessionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<BuilderSession?> GetSessionByIdAsync(Guid sessionId)
        {
            return await _context.BuilderSessions
                .Include(x => x.BaseKit) 
                .FirstOrDefaultAsync(x => x.Id == sessionId);
        }

        public async Task CreateSessionAsync(BuilderSession session)
        {
            await _context.BuilderSessions.AddAsync(session);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateSessionAsync(BuilderSession session)
        {
            _context.BuilderSessions.Update(session);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteSessionAsync(Guid sessionId)
        {
            var session = await _context.BuilderSessions.FindAsync(sessionId);
            if (session != null)
            {
                _context.BuilderSessions.Remove(session);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<BuilderSession?> GetActiveSessionByUserIdAsync(Guid userId, Guid baseKitId)
        {
            return await _context.BuilderSessions
                .Where(x => x.UserId == userId
                         && x.BaseKitId == baseKitId
                         && x.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<BuilderSession>> GetActiveSessionsByUserIdAsync(Guid userId)
        {
            // Logic: Lấy tất cả session của user mà CHƯA HẾT HẠN (ExpiresAt > Now)
            return await _context.BuilderSessions
                .Include(x => x.BaseKit) // Include để lấy tên và ảnh Kit
                .Where(x => x.UserId == userId && x.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(x => x.UpdatedAt)
                .ToListAsync();
        }
    }
}
