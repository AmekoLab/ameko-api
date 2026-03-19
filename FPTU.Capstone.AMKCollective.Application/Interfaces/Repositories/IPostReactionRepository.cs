using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IPostReactionRepository
    {
        Task<PostReaction?> GetByUserAndPostAsync(int postId, Guid userId, CancellationToken ct = default);
        Task<IEnumerable<PostReaction>> GetByPostAsync(int postId, CancellationToken ct = default);
        Task AddAsync(PostReaction reaction, CancellationToken ct = default);
        void Update(PostReaction reaction);
        void Remove(PostReaction reaction);
    }
}
