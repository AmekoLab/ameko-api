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
                .Include(p => p.User).ThenInclude(u => u.Role)
                .Include(p => p.User).ThenInclude(u => u.ShopProfile)
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
                .Include(p => p.User).ThenInclude(u => u.Role)
                .Include(p => p.User).ThenInclude(u => u.ShopProfile)
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
                .Include(p => p.User).ThenInclude(u => u.Role)
                .Include(p => p.User).ThenInclude(u => u.ShopProfile)
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
                .Include(p => p.User).ThenInclude(u => u.Role)
                .Include(p => p.User).ThenInclude(u => u.ShopProfile)
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

        public async Task<(List<CommunityPost> Items, int TotalCount)> GetPersonalizedFeedPagedAsync(Guid userId, List<Guid> purchasedShopIds, List<string> topSearchKeywords, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            // Phase 1: Only necessary field to calculate points
            var lightweightPosts = await _context.CommunityPosts
                .AsNoTracking()
                .Where(p => !p.IsDeleted)
                .OrderByDescending(p => p.CreatedAt)
                .Take(300)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.CreatedAt,
                    ShopId = p.User.ShopProfile != null ? p.User.ShopProfile.Id : (Guid?)null,
                    ShopRating = p.User.ShopProfile != null ? (double)p.User.ShopProfile.Rating : 0 // float/decimal to double if need
                })
                .ToListAsync(cancellationToken);

            var totalCount = lightweightPosts.Count;

            // Phase 2: Calculate points on RAM
            var scoredPosts = lightweightPosts.Select(p =>
            {
                double score = 0;

                // 1: Shop Rating (vd: Rating 5 star -> +15 Points)
                score += p.ShopRating * 3;

                // 2: Order history (+20 points if regular customer)
                if (p.ShopId.HasValue && purchasedShopIds.Contains(p.ShopId.Value))
                {
                    score += 20;
                }

                // 3: keyword search (+10 points if keyword matches)
                if (topSearchKeywords.Any() && !string.IsNullOrEmpty(p.Title))
                {
                    var titleLower = p.Title.ToLower();
                    foreach (var kw in topSearchKeywords)
                    {
                        if (titleLower.Contains(kw)) score += 10;
                    }
                }

                // 4: Freshness (older posts get lower scores, subtract 0.5 points per day)
                var daysOld = (DateTime.UtcNow - p.CreatedAt).TotalDays;
                score -= (daysOld * 0.5);

                return new { PostId = p.Id, TotalScore = score };
            });

            // Sort by score and select only the IDs of the top 10 posts to display (pageSize)
            var pagedPostIds = scoredPosts
                .OrderByDescending(x => x.TotalScore)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => x.PostId)
                .ToList();

            if (!pagedPostIds.Any()) return (new List<CommunityPost>(), totalCount);

            // Phase 3: Query full data ONLY for the selected 10 posts
            var finalPosts = await _context.CommunityPosts
                .Include(p => p.User)
                    .ThenInclude(u => u.ShopProfile)
                .Include(p => p.Attachments)
                .Include(p => p.PostReactions.Where(r => !r.IsDeleted))
                .Include(p => p.PostComments.Where(c => !c.IsDeleted))
                .Where(p => pagedPostIds.Contains(p.Id))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            // The .Where(Contains) query in SQL does not preserve the in-memory order,
            // need to re-sort `finalPosts` based on the calculated score order above.
            var orderedFinalPosts = pagedPostIds
                .Select(id => finalPosts.First(p => p.Id == id))
                .ToList();

            return (orderedFinalPosts, totalCount);
        }
    }
}
