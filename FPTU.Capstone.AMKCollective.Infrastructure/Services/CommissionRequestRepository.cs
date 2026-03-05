using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class CommissionRequestRepository : ICommissionRequestRepository
    {
        private readonly ApplicationDbContext _context;
        public CommissionRequestRepository(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<CommissionRequest?> GetByIdAsync(Guid id)
        {
            return await _context.CommissionRequests
                .Include(r => r.Quotes)
                    .ThenInclude(q => q.Shop)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<IEnumerable<CommissionRequest>> GetByUserIdAsync(Guid userId)
        {
            return await _context.CommissionRequests
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<CommissionRequest>> GetOpenPoolRequestsAsync()
        {
            return await _context.CommissionRequests
                .Where(r => r.Status == CommissionStatus.OpenPool)
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task AddAsync(CommissionRequest request)
        {
            await _context.CommissionRequests.AddAsync(request);
        }

        public async Task UpdateAsync(CommissionRequest request)
        {
            _context.CommissionRequests.Update(request);
            await Task.CompletedTask;
        }
        public async Task<IEnumerable<CommissionRequest>> GetTargetedRequestsForShopAsync(Guid shopId)
        {
            return await _context.CommissionRequests
                .Where(r => r.TargetedShopId == shopId && r.Status == CommissionStatus.PendingTarget)
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }
    }
}
