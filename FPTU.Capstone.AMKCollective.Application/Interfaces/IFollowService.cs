using FPTU.Capstone.AMKCollective.Application.DTOs.Follow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces
{
    public interface IFollowService
    {
        Task FollowUser(FollowRequest follow);
        Task UnfollowUser(FollowRequest unfollow);
        Task<IEnumerable<FollowResponse>> GetFollowsByFollower(Guid followerId);
        Task<FollowResponse?> GetFollowRecord(Guid followerId, Guid followedId);
    }
}
