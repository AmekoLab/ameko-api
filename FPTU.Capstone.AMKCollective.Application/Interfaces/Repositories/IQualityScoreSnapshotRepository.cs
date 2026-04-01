using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IQualityScoreSnapshotRepository
    {
        Task AddAsync(QualityScoreSnapshot snapshot);
        Task<QualityScoreSnapshot?> GetLatestSnapshotAsync(Guid shopId);
        Task<IEnumerable<QualityScoreSnapshot>> GetHistoryAsync(Guid shopId, DateTime fromDate);
    }
}
