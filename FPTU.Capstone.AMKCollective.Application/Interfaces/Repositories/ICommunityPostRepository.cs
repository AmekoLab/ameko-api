using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface ICommunityPostRepository
    {
        Task<List<CommunityPost>> GetFeedCursorPagedAsync(DateTime? cursorDate, int? cursorId, int pageSize, CancellationToken ct = default);
        Task<List<CommunityPost>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default);
        Task<CommunityPost?> GetByIdAsync(int id, CancellationToken ct = default);
        Task AddAsync(CommunityPost post, CancellationToken ct = default);
        void Update(CommunityPost post);
        void Remove(CommunityPost post);
    }
}
