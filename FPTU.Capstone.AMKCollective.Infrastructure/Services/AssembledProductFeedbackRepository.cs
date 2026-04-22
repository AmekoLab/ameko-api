using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class AssembledProductFeedbackRepository : IAssembledProductFeedbackRepository
    {
        private readonly ApplicationDbContext _context;

        public AssembledProductFeedbackRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AssembledProductFeedback?> GetByIdAsync(Guid feedbackId)
        {
            return await _context.AssembledProductFeedbacks
                .Include(f => f.Images)
                .Include(f => f.FromUser)
                .FirstOrDefaultAsync(f => f.Id == feedbackId);
        }

        public async Task<List<AssembledProductFeedback>> GetByOrderItemIdsAsync(IEnumerable<Guid> orderItemIds)
        {
            var ids = orderItemIds.Distinct().ToList();
            if (!ids.Any()) return new List<AssembledProductFeedback>();

            return await _context.AssembledProductFeedbacks
                .AsNoTracking()
                .Where(f => ids.Contains(f.OrderItemId))
                .Select(f => new AssembledProductFeedback
                {
                    Id = f.Id,
                    OrderItemId = f.OrderItemId
                })
                .ToListAsync();
        }

        public async Task<(IEnumerable<AssembledProductFeedback> Items, int TotalCount)> GetByProductIdAsync(Guid productId, int pageNumber, int pageSize)
        {
            var query = _context.AssembledProductFeedbacks
                .Include(f => f.Images)
                .Include(f => f.FromUser)
                .Where(f => f.AssembledProductId == productId);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(f => f.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<(IEnumerable<AssembledProductFeedback> Items, int TotalCount)> GetByShopIdAsync(Guid shopId, int pageNumber, int pageSize)
        {
            var query = _context.AssembledProductFeedbacks
                .Include(f => f.Images)
                .Include(f => f.FromUser)
                .Where(f => f.ShopId == shopId);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(f => f.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task AddAsync(AssembledProductFeedback feedback)
        {
            await _context.AssembledProductFeedbacks.AddAsync(feedback);
        }

        public void Update(AssembledProductFeedback feedback)
        {
            _context.AssembledProductFeedbacks.Update(feedback);
        }

        public async Task<bool> ExistsByOrderItemIdAsync(Guid orderItemId)
        {
            return await _context.AssembledProductFeedbacks.AnyAsync(f => f.OrderItemId == orderItemId);
        }
        public async Task<AssembledProductFeedback?> GetByOrderItemIdAsync(Guid orderItemId)
        {
            return await _context.AssembledProductFeedbacks
                .Include(f => f.Images)
                .Include(f => f.FromUser)
                .FirstOrDefaultAsync(f => f.OrderItemId == orderItemId);
        }
        public async Task RemoveOldImagesAsync(Guid feedbackId, IEnumerable<FeedbackImage> trackedImages)
        {
            // Xóa trực tiếp bằng SQL - bypass EF change tracking hoàn toàn
            await _context.FeedbackImages
                .Where(fi => fi.AssembledProductFeedbackId == feedbackId)
                .ExecuteDeleteAsync();

            // Detach các entity đã tracked để Clear() sau này không sinh ra UPDATE SET FK = NULL
            foreach (var img in trackedImages)
            {
                _context.Entry(img).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            }
        }

        public async Task AddImageAsync(FeedbackImage image)
        {
            // Add thẳng vào context, KHÔNG qua navigation collection
            // Tránh EF relationship fixup gây conflict tracking khi collection đã bị xoá ảnh cũ
            await _context.FeedbackImages.AddAsync(image);
        }
    }
}
