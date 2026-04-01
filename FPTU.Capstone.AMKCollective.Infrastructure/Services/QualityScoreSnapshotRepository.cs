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
    public class QualityScoreSnapshotRepository : IQualityScoreSnapshotRepository
    {
        private readonly ApplicationDbContext _context;

        public QualityScoreSnapshotRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(QualityScoreSnapshot snapshot)
        {
            await _context.QualityScoreSnapshots.AddAsync(snapshot);
        }
        public async Task<QualityScoreSnapshot?> GetLatestSnapshotAsync(Guid shopId)
        {
            return await _context.QualityScoreSnapshots
                .Where(s => s.ShopId == shopId)
                .OrderByDescending(s => s.CapturedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<QualityScoreSnapshot>> GetHistoryAsync(Guid shopId, DateTime fromDate)
        {
            return await _context.QualityScoreSnapshots
                .Where(s => s.ShopId == shopId && s.CapturedAt >= fromDate)
                .OrderBy(s => s.CapturedAt)
                .ToListAsync();
        }
    }
}
