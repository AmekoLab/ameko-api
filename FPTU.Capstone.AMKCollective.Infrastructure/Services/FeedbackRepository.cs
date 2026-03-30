using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class FeedbackRepository : IFeedbackRepository
    {
        private readonly ApplicationDbContext _context;

        public FeedbackRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Feedback?> GetByIdAsync(Guid feedbackId)
        {
            return await _context.Feedbacks
                .Include(f => f.Images)
                .Include(f => f.FromUser)
                .FirstOrDefaultAsync(f => f.Id == feedbackId);
        }

        public async Task<Feedback?> GetByOrderIdAsync(Guid orderId)
        {
            return await _context.Feedbacks
                .Include(f => f.Images)
                .FirstOrDefaultAsync(f => f.OrderId == orderId);
        }

        public async Task<IEnumerable<Feedback>> GetFeedbacksByShopIdAsync(Guid shopId)
        {
            return await _context.Feedbacks
                .Include(f => f.Images)
                .Include(f => f.FromUser)
                .Where(f => f.ShopId == shopId)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();
        }

        public async Task AddAsync(Feedback feedback)
        {
            await _context.Feedbacks.AddAsync(feedback);
        }

        public void Update(Feedback feedback)
        {
            _context.Feedbacks.Update(feedback);
        }

        public async Task<bool> ExistsByOrderIdAsync(Guid orderId)
        {
            return await _context.Feedbacks.AnyAsync(f => f.OrderId == orderId);
        }
    }
}