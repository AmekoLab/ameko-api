using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using FPTU.Capstone.AMKCollective.Application.Helpers;
using FPTU.Capstone.AMKCollective.Application.DTOs.Community;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using FPTU.Capstone.AMKCollective.Application.Exceptions;

namespace FPTU.Capstone.AMKCollective.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class SocialCommerceController : BaseApiController
    {
        private readonly ICommunityService _communityService;

        public SocialCommerceController(ICommunityService communityService)
        {
            _communityService = communityService;
        }

        /// <summary>
        /// Retrieves the paginated community posts feed.
        /// </summary>
        /// <param name="cursor">The cursor string used for pagination.</param>
        /// <param name="pageSize">The number of items to retrieve per page.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A paginated list of community posts.</returns>
        [HttpGet("posts/feed")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Get Community Feed", Description = "Retrieves the global community posts feed using cursor-based pagination.")]
        [SwaggerResponse(200, "Successfully retrieved feed", typeof(ApiResponse<CursorPagedResult<PostFeedResponse>>))]
        public async Task<IActionResult> GetFeed([FromQuery] string? cursor, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        {
            var feed = await _communityService.GetFeedAsync(TryGetCurrentUserId(), cursor, pageSize, cancellationToken);
            return SuccessResponse(feed);
        }

        /// <summary>
        /// Creates a new community post.
        /// </summary>
        /// <param name="request">The post details.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created post details.</returns>
        [HttpPost("posts")]
        [Authorize]
        [SwaggerOperation(Summary = "Create Community Post", Description = "Creates a new community post optionally linking to an assembled product.")]
        [SwaggerResponse(200, "Post created successfully", typeof(ApiResponse<PostFeedResponse>))]
        [SwaggerResponse(400, "Moderation failed", typeof(ApiResponse<ModerationResult>))]
        [SwaggerResponse(401, "Unauthorized")]
        public async Task<IActionResult> CreatePost([FromBody] CreatePostDto request, CancellationToken cancellationToken = default)
        {
            Guid userId = GetCurrentUserId();
            var post = await _communityService.CreatePostAsync(userId, request, cancellationToken);
            return SuccessResponse(post, "Post created successfully");
        }

        /// <summary>
        /// Retrieves a specific community post by ID.
        /// </summary>
        [HttpGet("posts/{id}")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Get Post by ID", Description = "Retrieves details of a specific community post.")]
        [SwaggerResponse(200, "Successfully retrieved post", typeof(ApiResponse<PostFeedResponse>))]
        [SwaggerResponse(404, "Post not found")]
        public async Task<IActionResult> GetPostById(int id, CancellationToken cancellationToken = default)
        {
            var post = await _communityService.GetPostByIdAsync(id, TryGetCurrentUserId(), cancellationToken);
            return SuccessResponse(post);
        }

        /// <summary>
        /// Updates an existing community post.
        /// </summary>
        [HttpPut("posts/{id}")]
        [Authorize]
        [SwaggerOperation(Summary = "Update Post", Description = "Updates an existing community post. Only the owner can update.")]
        [SwaggerResponse(200, "Post updated successfully", typeof(ApiResponse<PostFeedResponse>))]
        [SwaggerResponse(400, "Moderation failed", typeof(ApiResponse<ModerationResult>))]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(404, "Post not found")]
        public async Task<IActionResult> UpdatePost(int id, [FromBody] UpdatePostDto request, CancellationToken cancellationToken = default)
        {
            Guid userId = GetCurrentUserId();
            var post = await _communityService.UpdatePostAsync(id, userId, request, cancellationToken);
            return SuccessResponse(post, "Post updated successfully");
        }

        /// <summary>
        /// Deletes a community post.
        /// </summary>
        [HttpDelete("posts/{id}")]
        [Authorize]
        [SwaggerOperation(Summary = "Delete Post", Description = "Deletes a community post. Only the owner can delete.")]
        [SwaggerResponse(200, "Post deleted successfully")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(404, "Post not found")]
        public async Task<IActionResult> DeletePost(int id, CancellationToken cancellationToken = default)
        {
            Guid userId = GetCurrentUserId();
            await _communityService.DeletePostAsync(id, userId, cancellationToken);
            return SuccessResponse(new { Id = id }, "Post deleted successfully");
        }

        [HttpPost("posts/{postId}/react")]
        [Authorize]
        [SwaggerOperation(Summary = "React to Post", Description = "Toggles a reaction or updates it. Types: Like, Love, Haha, Wow, Sad, Angry.")]
        public async Task<IActionResult> ReactToPost(int postId, [FromBody] ReactToPostDto request, CancellationToken cancellationToken = default)
        {
            Guid userId = GetCurrentUserId();
            if (!Enum.TryParse<ReactionType>(request.Type, true, out var reactionType))
            {
                return ErrorResponse<string>("Invalid reaction type");
            }

            await _communityService.ReactToPostAsync(postId, userId, reactionType, cancellationToken);
            return SuccessResponse(new { success = true });
        }
        [HttpDelete("posts/{postId}/reactions")]
        [Authorize]
        [SwaggerOperation(Summary = "Soft Delete Reaction", Description = "Removes the current user's reaction from a specific post (Soft Delete).")]
        [SwaggerResponse(200, "Reaction hidden successfully")]
        [SwaggerResponse(401, "Unauthorized")]
        public async Task<IActionResult> UnreactToPost(int postId, CancellationToken cancellationToken = default)
        {
            Guid userId = GetCurrentUserId();
            await _communityService.RemoveReactionAsync(postId, userId, cancellationToken);
            return SuccessResponse(new { success = true });
        }

        [HttpDelete("posts/{postId}/reactions/{targetUserId}/hard")]
        [Authorize]
        [SwaggerOperation(Summary = "Hard Delete Reaction (Admin Only)", Description = "Permanently deletes a reaction. Only accessible to Admins.")]
        [SwaggerResponse(200, "Reaction deleted permanently")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden")]
        public async Task<IActionResult> HardDeleteReaction(int postId, Guid targetUserId, CancellationToken cancellationToken = default)
        {
            Guid adminUserId = GetCurrentUserId();
            string role = GetCurrentUserRole();
            await _communityService.HardDeleteReactionAsync(postId, targetUserId, role, cancellationToken);
            return SuccessResponse(new { success = true }, "Reaction deleted permanently");
        }

        [HttpGet("posts/{postId}/reactions")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Get Post Reactions", Description = "Retrieves a detailed list of reactions for a specific post.")]
        [SwaggerResponse(200, "Successfully retrieved reactions", typeof(ApiResponse<IEnumerable<PostReactionDetailResponse>>))]
        public async Task<IActionResult> GetPostReactions(int postId, CancellationToken cancellationToken = default)
        {
            var reactions = await _communityService.GetPostReactionsAsync(postId, cancellationToken);
            return SuccessResponse(reactions);
        }

        [HttpGet("users/{userId}/posts")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Get User Posts", Description = "Retrieves all posts created by a specific user.")]
        [SwaggerResponse(200, "Successfully retrieved user posts", typeof(ApiResponse<CursorPagedResult<PostFeedResponse>>))]
        public async Task<IActionResult> GetUserPosts(Guid userId, [FromQuery] string? cursor, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
        {
            var results = await _communityService.GetPostsByUserIdAsync(userId, TryGetCurrentUserId(), cursor, pageSize, cancellationToken);
            return SuccessResponse(results);
        }

        [HttpPost("posts/{postId}/comments")]
        [Authorize]
        [SwaggerOperation(Summary = "Add Comment", Description = "Adds a new comment to a post. Subject to moderation and rate limiting (20/min).")]
        [SwaggerResponse(200, "Comment added successfully", typeof(ApiResponse<CommentResponse>))]
        [SwaggerResponse(400, "Moderation failed", typeof(ApiResponse<ModerationResult>))]
        public async Task<IActionResult> AddComment(int postId, [FromBody] CreateCommentDto request, CancellationToken cancellationToken = default)
        {
            try
            {
                Guid userId = GetCurrentUserId();
                var comment = await _communityService.AddCommentAsync(postId, userId, request, cancellationToken);
                return SuccessResponse(comment, "Comment added successfully");
            }
            catch (InvalidOperationException ex)
            {
                return ErrorResponse<string>(ex.Message);
            }
        }

        [HttpGet("posts/{postId}/comments")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Get Post Comments", Description = "Retrieves all comments for a specific post.")]
        [SwaggerResponse(200, "Successfully retrieved comments", typeof(ApiResponse<CursorPagedResult<CommentResponse>>))]
        public async Task<IActionResult> GetPostComments(int postId, [FromQuery] string? cursor, [FromQuery] int pageSize = 5, CancellationToken cancellationToken = default)
        {
            var comments = await _communityService.GetPostCommentsAsync(postId, cursor, pageSize, cancellationToken);
            return SuccessResponse(comments);
        }

        [HttpPut("comments/{commentId}")]
        [Authorize]
        [SwaggerOperation(Summary = "Edit Comment", Description = "Edits an existing comment. Preserves edit history.")]
        [SwaggerResponse(200, "Comment updated successfully", typeof(ApiResponse<CommentResponse>))]
        public async Task<IActionResult> UpdateComment(int commentId, [FromBody] UpdateCommentDto request, CancellationToken cancellationToken = default)
        {
            try
            {
                Guid userId = GetCurrentUserId();
                var result = await _communityService.UpdateCommentAsync(commentId, userId, request, cancellationToken);
                return SuccessResponse(result, "Comment updated successfully");
            }
            catch (UnauthorizedAccessException ex)
            {
                return ForbiddenResponse<object>(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ErrorResponse<string>(ex.Message);
            }
        }
        [HttpDelete("comments/{commentId}/soft-delete")]
        [Authorize]
        [SwaggerOperation(Summary = "Soft Delete Comment (Owner/Admin)", Description = "BR: Hides a comment so it doesn't appear in feeds. Accessible to the Comment Author, the Post Author, and Admins.")]
        [SwaggerResponse(200, "Comment hidden successfully")]
        public async Task<IActionResult> SoftDeleteComment(int commentId, CancellationToken cancellationToken = default)
        {
            Guid userId = GetCurrentUserId();
            string role = GetCurrentUserRole();
            await _communityService.SoftDeleteCommentAsync(commentId, userId, role, cancellationToken);
            return SuccessResponse("Comment hidden successfully");
        }

        [HttpDelete("comments/{commentId}")]
        [Authorize]
        [SwaggerOperation(Summary = "Hard Delete Comment (Admin Only)", Description = "BR: Deletes a comment completely from the database. Only accessible to Admins.")]
        [SwaggerResponse(200, "Comment deleted successfully")]
        public async Task<IActionResult> DeleteComment(int commentId, CancellationToken cancellationToken = default)
        {
            Guid userId = GetCurrentUserId();
            string role = GetCurrentUserRole();
            await _communityService.HardDeleteCommentAsync(commentId, userId, role, cancellationToken);
            return SuccessResponse("Comment deleted permanently");
        }
    }
}
