using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class FollowRepository : IFollowRepository
    {
        private readonly ApplicationDbContext _context;

        public FollowRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public void FollowUser(Follow follow)
        {
            _context.Follows.Add(follow);
        }

        public async Task<IEnumerable<Follow>> GetByFollower(Guid followerId)
        {
            return await _context.Follows
                .Include(f => f.Followed)
                .Where(f => f.FollowerId == followerId)
                .ToListAsync();
        }

        public async Task<IEnumerable<Follow>> GetByFollowedId(Guid followedId)
        {
            return await _context.Follows
                .Include(f => f.Follower)
                .Where(f => f.FollowedId == followedId)
                .ToListAsync();
        }

        public void UnfollowUser(Follow follow)
        {
            _context.Follows.Remove(follow);
        }

        public async Task<Follow?> GetFollowRecord(Guid followerId, Guid followedId)
        {
            return await _context.Follows.FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FollowedId == followedId);
        }
    }
}
