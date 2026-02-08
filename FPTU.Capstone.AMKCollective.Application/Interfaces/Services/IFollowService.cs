using FPTU.Capstone.AMKCollective.Application.DTOs.Follow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IFollowService
    {
        Task FollowUser(FollowRequest follow);
        Task UnfollowUser(FollowRequest unfollow);
        Task ToggleFollowUser(FollowRequest request);
        Task<IEnumerable<FollowResponse>> GetFollowsByFollower(Guid followerId);
        Task<IEnumerable<FollowedUserResponse>> GetFollowedUsersByFollower(Guid followerId);
        Task<IEnumerable<FollowerResponse>> GetFollowersByUserId(Guid userId);
        Task<FollowResponse?> GetFollowRecord(Guid followerId, Guid followedId);
    }
}
