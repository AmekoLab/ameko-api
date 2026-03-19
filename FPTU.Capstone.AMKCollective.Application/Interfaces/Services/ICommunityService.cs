using System;
using System.Threading;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Application.Helpers;
using FPTU.Capstone.AMKCollective.Application.DTOs.Community;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface ICommunityService
    {
        Task<PostFeedResponse> CreatePostAsync(Guid userId, CreatePostDto request, CancellationToken cancellationToken = default);
        Task<CursorPagedResult<PostFeedResponse>> GetFeedAsync(string? cursor, int pageSize, CancellationToken cancellationToken = default);
        Task<PostFeedResponse> GetPostByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<PostFeedResponse> UpdatePostAsync(int id, Guid userId, UpdatePostDto request, CancellationToken cancellationToken = default);
        Task DeletePostAsync(int id, Guid userId, CancellationToken cancellationToken = default);
        Task ReactToPostAsync(int postId, Guid userId, FPTU.Capstone.AMKCollective.Domain.Enums.ReactionType type, CancellationToken cancellationToken = default);
        Task<IEnumerable<PostReactionDetailResponse>> GetPostReactionsAsync(int postId, CancellationToken cancellationToken = default);
    }
}
