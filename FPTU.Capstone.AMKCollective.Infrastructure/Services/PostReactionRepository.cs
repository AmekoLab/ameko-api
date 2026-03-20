using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class PostReactionRepository : IPostReactionRepository
    {
        private readonly ApplicationDbContext _context;

        public PostReactionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PostReaction?> GetByUserAndPostAsync(int postId, Guid userId, CancellationToken ct = default)
        {
            return await _context.PostReactions
                .FirstOrDefaultAsync(pr => pr.PostId == postId && pr.UserId == userId, ct);
        }

        public async Task<IEnumerable<PostReaction>> GetByPostAsync(int postId, CancellationToken ct = default)
        {
            return await _context.PostReactions
                .Include(pr => pr.User)
                .Where(pr => pr.PostId == postId)
                .OrderByDescending(pr => pr.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task AddAsync(PostReaction reaction, CancellationToken ct = default)
        {
            await _context.PostReactions.AddAsync(reaction, ct);
        }

        public void Update(PostReaction reaction)
        {
            _context.PostReactions.Update(reaction);
        }

        public void Remove(PostReaction reaction)
        {
            _context.PostReactions.Remove(reaction);
        }
    }
}
