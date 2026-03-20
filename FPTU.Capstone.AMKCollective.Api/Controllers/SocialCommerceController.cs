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
        [SwaggerResponse(200, "Successfully retrieved feed", typeof(CursorPagedResult<PostFeedResponse>))]
        public async Task<IActionResult> GetFeed([FromQuery] string? cursor, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        {
            var feed = await _communityService.GetFeedAsync(cursor, pageSize, cancellationToken);
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
        [SwaggerResponse(200, "Post created successfully", typeof(PostFeedResponse))]
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
        [SwaggerResponse(200, "Successfully retrieved post", typeof(PostFeedResponse))]
        [SwaggerResponse(404, "Post not found")]
        public async Task<IActionResult> GetPostById(int id, CancellationToken cancellationToken = default)
        {
            var post = await _communityService.GetPostByIdAsync(id, cancellationToken);
            return SuccessResponse(post);
        }

        /// <summary>
        /// Updates an existing community post.
        /// </summary>
        [HttpPut("posts/{id}")]
        [Authorize]
        [SwaggerOperation(Summary = "Update Post", Description = "Updates an existing community post. Only the owner can update.")]
        [SwaggerResponse(200, "Post updated successfully", typeof(PostFeedResponse))]
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
                return ErrorResponse("Invalid reaction type");
            }

            await _communityService.ReactToPostAsync(postId, userId, reactionType, cancellationToken);
            return SuccessResponse(new { success = true });
        }

        [HttpGet("posts/{postId}/reactions")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Get Post Reactions", Description = "Retrieves a detailed list of reactions for a specific post.")]
        [SwaggerResponse(200, "Successfully retrieved reactions", typeof(IEnumerable<PostReactionDetailResponse>))]
        public async Task<IActionResult> GetPostReactions(int postId, CancellationToken cancellationToken = default)
        {
            var reactions = await _communityService.GetPostReactionsAsync(postId, cancellationToken);
            return SuccessResponse(reactions);
        }
    }
}
