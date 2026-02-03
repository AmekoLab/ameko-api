using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IFollowRepository
    {
        void FollowUser(Follow follow); 
        void UnfollowUser(Follow follow); 
        Task<IEnumerable<Follow>> GetByFollower(Guid followerId);
        Task<Follow?> GetFollowRecord(Guid followerId, Guid followedId);
    }
}
