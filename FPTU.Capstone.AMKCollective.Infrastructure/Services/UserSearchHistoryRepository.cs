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
    public class UserSearchHistoryRepository : IUserSearchHistoryRepository
    {
        private readonly ApplicationDbContext _context;
        public UserSearchHistoryRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogSearchAsync(Guid userId, string keyword, string searchType, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return;

            var keywordLower = keyword.Trim().ToLower();

            var existingRecord = await _context.UserSearchHistories
                .FirstOrDefaultAsync(x => x.UserId == userId
                                       && x.Keyword.ToLower() == keywordLower
                                       && x.SearchType == searchType, cancellationToken);

            if (existingRecord != null)
            {
                existingRecord.SearchCount += 1;
                existingRecord.LastSearchedAt = DateTime.UtcNow;
                _context.UserSearchHistories.Update(existingRecord);
            }
            else
            {
                var newRecord = new UserSearchHistory
                {
                    UserId = userId,
                    Keyword = keywordLower,
                    SearchType = searchType,
                    SearchCount = 1,
                    LastSearchedAt = DateTime.UtcNow
                };
                await _context.UserSearchHistories.AddAsync(newRecord, cancellationToken);
            }
        }

        public async Task<List<UserSearchHistory>> GetTopSearchesByUserAsync(Guid userId, int limit = 10, CancellationToken cancellationToken = default)
        {
            return await _context.UserSearchHistories
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.LastSearchedAt)
                .ThenByDescending(x => x.SearchCount)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }
    }
}