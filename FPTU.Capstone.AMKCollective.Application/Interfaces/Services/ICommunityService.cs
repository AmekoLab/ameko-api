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
        Task<CursorPagedResult<PostFeedResponse>> GetFeedAsync(Guid? currentUserId, string? cursor, int pageSize, CancellationToken cancellationToken = default);
        Task<PostFeedResponse> GetPostByIdAsync(int id, Guid? currentUserId, CancellationToken cancellationToken = default);
        Task<PostFeedResponse> UpdatePostAsync(int id, Guid userId, UpdatePostDto request, CancellationToken cancellationToken = default);
        Task DeletePostAsync(int id, Guid userId, CancellationToken cancellationToken = default);
        Task ReactToPostAsync(int postId, Guid userId, FPTU.Capstone.AMKCollective.Domain.Enums.ReactionType type, CancellationToken cancellationToken = default);
        Task RemoveReactionAsync(int postId, Guid userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<PostReactionDetailResponse>> GetPostReactionsAsync(int postId, CancellationToken cancellationToken = default);
        Task<CursorPagedResult<PostFeedResponse>> GetPostsByUserIdAsync(Guid userId, Guid? currentUserId, string? cursor, int pageSize, CancellationToken cancellationToken = default);
        Task<CommentResponse> AddCommentAsync(int postId, Guid userId, CreateCommentDto request, CancellationToken cancellationToken = default);
        Task<CursorPagedResult<CommentResponse>> GetPostCommentsAsync(int postId, string? cursor, int pageSize, CancellationToken cancellationToken = default);
        Task<CommentResponse> UpdateCommentAsync(int commentId, Guid userId, UpdateCommentDto request, CancellationToken cancellationToken = default);
        Task SoftDeleteCommentAsync(int commentId, Guid userId, string userRole, CancellationToken cancellationToken = default);
        Task HardDeleteCommentAsync(int commentId, Guid userId, string userRole, CancellationToken cancellationToken = default);
        Task HardDeleteReactionAsync(int postId, Guid userId, string userRole, CancellationToken cancellationToken = default);
        Task<PaginatedResult<PostFeedResponse>> GetPersonalizedFeedAsync(Guid currentUserId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    }
}
