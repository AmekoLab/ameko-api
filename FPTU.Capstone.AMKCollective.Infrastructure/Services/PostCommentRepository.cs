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
    public class PostCommentRepository : IPostCommentRepository
    {
        private readonly ApplicationDbContext _context;

        public PostCommentRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<PostComment>> GetByPostIdAsync(int postId, CancellationToken ct = default)
        {
            return await _context.PostComments
                .Where(c => c.PostId == postId && !c.IsDeleted)
                .Include(c => c.User)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<List<PostComment>> GetByPostIdCursorPagedAsync(int postId, DateTime? createdAt, int? id, int pageSize, CancellationToken ct = default)
        {
            var query = _context.PostComments
                .Where(c => c.PostId == postId && !c.IsDeleted)
                .AsQueryable();

            if (createdAt.HasValue && id.HasValue)
            {
                query = query.Where(c => c.CreatedAt < createdAt.Value || (c.CreatedAt == createdAt.Value && c.Id < id.Value));
            }

            return await query
                .OrderByDescending(c => c.CreatedAt)
                .ThenByDescending(c => c.Id)
                .Take(pageSize + 1)
                .Include(c => c.User)
                .ToListAsync(ct);
        }

        public async Task<PostComment?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.PostComments
                .Include(c => c.User)
                .Include(c => c.Post)
                .FirstOrDefaultAsync(c => c.Id == id, ct);
        }

        public async Task AddAsync(PostComment comment, CancellationToken ct = default)
        {
            await _context.PostComments.AddAsync(comment, ct);
        }

        public void Update(PostComment comment)
        {
            _context.PostComments.Update(comment);
        }

        public void Remove(PostComment comment)
        {
            _context.PostComments.Remove(comment);
        }
    }
}
