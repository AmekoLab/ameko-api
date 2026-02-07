using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using FPTU.Capstone.AMKCollective.Application.DTOs.Follow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;

    /// <summary>
    /// API Controller for handling follow/unfollow functionality between users
    /// </summary>
    [Route("api/v1/[controller]")]
    [ApiController]
    [Authorize]
    public class FollowsController : BaseApiController
    {
        private readonly IFollowService _followService;

        /// <summary>
        /// Initializes FollowsController with dependency injection
        /// </summary>
        /// <param name="followService">Service for follow logic</param>
        public FollowsController(IFollowService followService)
        {
            _followService = followService;
        }

        /// <summary>
        /// Current user follows another user
        /// </summary>
        /// <param name="followedId">ID of the user to follow</param>
        /// <returns>Follow success message</returns>
        [HttpPost]
        [SwaggerOperation(Summary = "Follow a user", Description = "Send a follow request to another user. User ID is extracted from the token.")]
        [SwaggerResponse(200, "Followed successfully", typeof(ApiResponse<object>))]
        [SwaggerResponse(401, "Unauthorized access")]
        public async Task<IActionResult> FollowUser([FromBody] Guid followedId)
        {
            var followerId = GetCurrentUserId();
            var followRequest = new FollowRequest 
            { 
                FollowerId = followerId, 
                FollowedId = followedId 
            };
            await _followService.FollowUser(followRequest);
            return SuccessResponse("Followed successfully");
        }

        /// <summary>
        /// Current user unfollows another user
        /// </summary>
        /// <param name="followedId">ID of the user to unfollow</param>
        /// <returns>Unfollow success message</returns>
        [HttpPost("unfollow")]
        [SwaggerOperation(Summary = "Unfollow a user", Description = "Remove a follow relationship with another user. User ID is extracted from the token.")]
        [SwaggerResponse(200, "Unfollowed successfully", typeof(ApiResponse<object>))]
        [SwaggerResponse(401, "Unauthorized access")]
        public async Task<IActionResult> UnfollowUser([FromBody] Guid followedId)
        {
            var followerId = GetCurrentUserId();
            var unfollowRequest = new FollowRequest 
            { 
                FollowerId = followerId, 
                FollowedId = followedId 
            };
            await _followService.UnfollowUser(unfollowRequest);
            return SuccessResponse("Unfollowed successfully");
        }

        /// <summary>
        /// Get the list of users the current user is following
        /// </summary>
        /// <returns>List of followed users</returns>
        [HttpGet]
        [SwaggerOperation(Summary = "Get followed users", Description = "Retrieve a list of users that the current user is following. User ID is extracted from the token.")]
        [SwaggerResponse(200, "List of followed users retrieved successfully", typeof(ApiResponse<IEnumerable<FollowResponse>>))]
        [SwaggerResponse(401, "Unauthorized access")]
        public async Task<IActionResult> GetFollowsByCurrentUser()
        {
            var followerId = GetCurrentUserId();
            var follows = await _followService.GetFollowsByFollower(followerId);
            return SuccessResponse(follows, "List of users this follower is following");
        }

        /// <summary>
        /// Get the list of users a specific user is following
        /// </summary>
        /// <param name="followerId">ID of the user to check</param>
        /// <returns>List of users followed by that user</returns>
        [HttpGet("follower/{followerId}")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Get followed users by follower ID", Description = "Retrieve a list of users that a specific follower is following.")]
        [SwaggerResponse(200, "List of followed users retrieved successfully", typeof(ApiResponse<IEnumerable<FollowResponse>>))]
        public async Task<IActionResult> GetFollowsByFollower(Guid followerId)
        {
            var follows = await _followService.GetFollowsByFollower(followerId);
            return SuccessResponse(follows, "List of users this follower is following");
        }

        /// <summary>
        /// Check follow relationship between two users
        /// </summary>
        /// <param name="followerId">ID of the follower</param>
        /// <param name="followedId">ID of the followed user</param>
        /// <returns>Follow relationship info if exists</returns>
        [HttpGet("record")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Get follow record between follower and followed", Description = "Retrieve the follow relationship record between a specific follower and followed user.")]
        [SwaggerResponse(200, "Follow relationship record retrieved successfully", typeof(ApiResponse<FollowResponse>))]
        public async Task<IActionResult> GetFollowRecord([FromQuery] Guid followerId, [FromQuery] Guid followedId)
        {
            var followRecord = await _followService.GetFollowRecord(followerId, followedId);
            if (followRecord == null)
            {
                return NotFound();
            }
            return SuccessResponse(followRecord, "Follow relationship record between follower and followed user");
        }
    }
}

