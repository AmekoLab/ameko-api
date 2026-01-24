using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Follow;
using FPTU.Capstone.AMKCollective.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
	using Swashbuckle.AspNetCore.Annotations;
    [Route("api/v1/[controller]")]
    [ApiController]
    public class FollowsController : BaseApiController
    {
        private readonly IFollowService _followService;
        public FollowsController(IFollowService followService)
        {
            _followService = followService;
        }

        [HttpPost]
        [SwaggerOperation(Summary = "Follow a user", Description = "Send a follow request to another user.")]
        [SwaggerResponse(200, "Followed successfully", typeof(ApiResponse<object>))]
        public async Task<IActionResult> FollowUser([FromBody] FollowRequest followRequest)
        {
            await _followService.FollowUser(followRequest);
            return SuccessResponse("Followed successfully");
        }

        [HttpPost("unfollow")]
        [SwaggerOperation(Summary = "Unfollow a user", Description = "Remove a follow relationship with another user.")]
        [SwaggerResponse(200, "Unfollowed successfully", typeof(ApiResponse<object>))]
        public async Task<IActionResult> UnfollowUser([FromBody] FollowRequest unfollowRequest)
        {
            try
            {
                await _followService.UnfollowUser(unfollowRequest);
                return SuccessResponse("Unfollowed successfully");
            }
            catch (InvalidOperationException ex)
            {
                return ErrorResponse<string>(ex.Message);
            }
        }

        [HttpGet("follower/{followerId}")]
        [SwaggerOperation(Summary = "Get followed users by follower ID", Description = "Retrieve a list of users that a specific follower is following.")]
        [SwaggerResponse(200, "List of followed users retrieved successfully", typeof(ApiResponse<IEnumerable<FollowResponse>>))]
        public async Task<IActionResult> GetFollowsByFollower(Guid followerId)
        {
            var follows = await _followService.GetFollowsByFollower(followerId);
            return SuccessResponse(follows, "List of users this follower is following");
        }

        [HttpGet("record")]
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

