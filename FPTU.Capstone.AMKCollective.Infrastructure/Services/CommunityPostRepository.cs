using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class CommunityPostRepository : ICommunityPostRepository
    {
        private readonly ApplicationDbContext _context;

        public CommunityPostRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<CommunityPost>> GetFeedCursorPagedAsync(DateTime? cursorDate, int? cursorId, int pageSize, CancellationToken ct = default)
        {
            var query = _context.CommunityPosts
                .Include(p => p.Attachments)
                .Include(p => p.PostReactions)
                .Include(p => p.PostComments)
                .AsNoTracking();

            if (cursorDate.HasValue && cursorId.HasValue)
            {
                var cDate = cursorDate.Value;
                var cId = cursorId.Value;
                query = query.Where(n =>
                    n.CreatedAt < cDate ||
                    (n.CreatedAt == cDate && n.Id < cId)
                );
            }

            return await query
                .OrderByDescending(n => n.CreatedAt)
                .ThenByDescending(n => n.Id)
                .Take(pageSize + 1)
                .ToListAsync(ct);
        }

        public async Task<List<CommunityPost>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        {
            return await _context.CommunityPosts
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Include(p => p.Attachments)
                .Include(p => p.PostReactions)
                .Include(p => p.PostComments)
                .ToListAsync(ct);
        }

        public async Task<List<CommunityPost>> GetByUserIdCursorPagedAsync(Guid userId, DateTime? createdAt, int? id, int pageSize, CancellationToken ct = default)
        {
            var query = _context.CommunityPosts
                .Where(p => p.UserId == userId)
                .AsQueryable();

            if (createdAt.HasValue && id.HasValue)
            {
                query = query.Where(p => p.CreatedAt < createdAt.Value || (p.CreatedAt == createdAt.Value && p.Id < id.Value));
            }

            return await query
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Take(pageSize + 1)
                .Include(p => p.Attachments)
                .Include(p => p.PostReactions)
                .Include(p => p.PostComments)
                .ToListAsync(ct);
        }

        public async Task<List<CommunityPost>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
        {
            return await _context.CommunityPosts
                .Where(p => ids.Contains(p.Id))
                .ToListAsync(ct);
        }

        public async Task AddAsync(CommunityPost post, CancellationToken ct = default)
        {
            await _context.CommunityPosts.AddAsync(post, ct);
        }

        public async Task<CommunityPost?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.CommunityPosts
                .Include(p => p.Attachments)
                .Include(p => p.PostReactions)
                .Include(p => p.PostComments)
                .FirstOrDefaultAsync(p => p.Id == id, ct);
        }

        public void Update(CommunityPost post)
        {
            _context.CommunityPosts.Update(post);
        }

        public void Remove(CommunityPost post)
        {
            _context.CommunityPosts.Remove(post);
        }
    }
}
