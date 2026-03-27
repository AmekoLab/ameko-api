using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IPostCommentRepository
    {
        Task<List<PostComment>> GetByPostIdAsync(int postId, CancellationToken ct = default);
        Task<List<PostComment>> GetByPostIdCursorPagedAsync(int postId, DateTime? createdAt, int? id, int pageSize, CancellationToken ct = default);
        Task AddAsync(PostComment comment, CancellationToken ct = default);
        void Update(PostComment comment);
        void Remove(PostComment comment);
    }
}
