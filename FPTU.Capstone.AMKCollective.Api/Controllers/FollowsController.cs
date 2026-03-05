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
    /// 
    /// KEY CONCEPTS FOR FRONTEND:
    /// - FOLLOWING (Đang theo dõi): Users that YOUR account is following
    /// - FOLLOWERS (Người theo dõi): Users that are following YOUR account
    /// 
    /// ENDPOINTS SUMMARY:
    /// 1. POST /toggle - Follow/Unfollow someone
    /// 2. GET / - Get list of users YOU are following (YOUR Following list)
    /// 3. POST /check - Check if YOU are following a specific user
    /// 4. GET /followers/{userId} - Get list of users following that user (That user's Followers list)
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
        /// Toggle follow/unfollow for a user
        /// 
        /// FE INSTRUCTION:
        /// Send the ID of the user you want to follow/unfollow as 'followedId'
        /// This endpoint automatically detects if you're already following that user:
        /// - If YES: Unfollows them
        /// - If NO: Follows them
        /// </summary>
        /// <param name="request">Request containing followedId (ID of user to follow/unfollow)</param>
        /// <returns>Follow/unfollow result message ("Followed successfully" or "Unfollowed successfully")</returns>
        [HttpPost("toggle")]
        [SwaggerOperation(Summary = "Toggle follow/unfollow - Smart endpoint that auto-follows or unfollows", Description = "Follow a user if not already following, or unfollow if already following. Determined automatically based on current relationship.")]
        [SwaggerResponse(200, "Toggled successfully", typeof(ApiResponse<object>))]
        [SwaggerResponse(401, "Unauthorized access")]
        public async Task<IActionResult> ToggleFollowUser([FromBody] FollowRequestDto request)
        {
            try
            {
                var followerId = GetCurrentUserId();
                var followRequest = new FollowRequest 
                { 
                    FollowerId = followerId, 
                    FollowedId = request.FollowedId 
                };
                var existingFollow = await _followService.GetFollowRecord(followerId, request.FollowedId);
                var message = existingFollow != null ? "Unfollowed successfully" : "Followed successfully";
                await _followService.ToggleFollowUser(followRequest);
                return SuccessResponse(message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<string>("Error toggling follow");
            }
        }

        /// <summary>
        /// Get the list of users the current user is FOLLOWING (YOUR Following List)
        /// 
        /// FE INSTRUCTION:
        /// Use this to display "Following" tab on user's profile
        /// Shows all users that the logged-in user is following
        /// </summary>
        /// <returns>List of users that YOU are following (FollowedUserResponse with their IDs)</returns>
        [HttpGet]
        [SwaggerOperation(Summary = "Get MY following list", Description = "Retrieve a list of users that the current user (from token) is following. Requires authentication.")]
        [SwaggerResponse(200, "List of followed users retrieved successfully", typeof(ApiResponse<IEnumerable<FollowedUserResponse>>))]
        [SwaggerResponse(401, "Unauthorized access")]
        public async Task<IActionResult> GetFollowsByCurrentUser()
        {
            var followerId = GetCurrentUserId();
            var followedUsers = await _followService.GetFollowedUsersByFollower(followerId);
            return SuccessResponse(followedUsers, "List of users this follower is following");
        }


        /// <summary>
        /// Check if current user follows a specific user
        /// 
        /// FE INSTRUCTION:
        /// Use this to determine if you should show "Follow" or "Unfollow" button
        /// Returns { isFollowing: true/false } based on whether you're following that user
        /// </summary>
        /// <param name="request">Request containing userId (ID of user to check if you follow)</param>
        /// <returns>{ isFollowing: true } if current user follows the user, { isFollowing: false } otherwise</returns>
        [HttpPost("check")]
        [Authorize]
        [SwaggerOperation(Summary = "Check if I'm following a user", Description = "Returns true/false if the current user (from token) is following the specified user.")]
        [SwaggerResponse(200, "Follow relationship check result", typeof(ApiResponse<object>))]
        [SwaggerResponse(401, "Unauthorized access")]
        public async Task<IActionResult> CheckIfFollowing([FromBody] CheckFollowingRequestDto request)
        {
            var currentUserId = GetCurrentUserId();
            var follows = await _followService.GetFollowRecord(currentUserId, request.UserId);
            return SuccessResponse(new { isFollowing = follows != null });
        }

        /// <summary>
        /// Get list of users following a specific user (That user's Followers List)
        /// 
        /// FE INSTRUCTION:
        /// Use this to display "Followers" tab on any user's profile
        /// Pass any userId (can be current user or any other user) to see their followers
        /// This endpoint is public (AllowAnonymous) - can be called without login
        /// </summary>
        /// <param name="userId">ID of the user whose followers you want to see</param>
        /// <returns>List of users following that user (FollowerResponse with their IDs)</returns>
        [HttpGet("followers/{userId}")]
        [AllowAnonymous]
        [SwaggerOperation(Summary = "Get followers of any user - PUBLIC endpoint", Description = "Retrieve a list of users that are following the specified user. Can pass any userId - no authentication required.")]
        [SwaggerResponse(200, "List of followers retrieved successfully", typeof(ApiResponse<IEnumerable<FollowerResponse>>))]
        public async Task<IActionResult> GetFollowersByUserId(Guid userId)
        {
            var followers = await _followService.GetFollowersByUserId(userId);
            return SuccessResponse(followers, "List of followers of this user");
        }
    }
}

