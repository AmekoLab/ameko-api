using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs.Follow;
using FPTU.Capstone.AMKCollective.Application.Interfaces;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class FollowService : IFollowService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public FollowService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task FollowUser(FollowRequest follow)
        {
            var existingFollow = await _unitOfWork.Follows.GetFollowRecord(follow.FollowerId, follow.FollowedId);
            if (existingFollow != null)
            {
                // Follow relationship already exists; no-op to avoid unique constraint violation.
                return;
            }
            _unitOfWork.Follows.FollowUser(_mapper.Map<Follow>(follow));
            await _unitOfWork.CommitAsync();
        }

        public async Task UnfollowUser(FollowRequest unfollow)
        {
            var existingFollow = await _unitOfWork.Follows.GetFollowRecord(unfollow.FollowerId, unfollow.FollowedId);
            if (existingFollow == null)
            {
                throw new InvalidOperationException("Follow relationship not found.");
            }
            _unitOfWork.Follows.UnfollowUser(existingFollow);
            await _unitOfWork.CommitAsync();
        }

        // This method to get follows by follower
        public async Task<IEnumerable<FollowResponse>> GetFollowsByFollower(Guid followerId)
        {
            var follows = await _unitOfWork.Follows.GetByFollower(followerId);
            return _mapper.Map<IEnumerable<FollowResponse>>(follows);
        }

        public async Task<FollowResponse?> GetFollowRecord(Guid followerId, Guid followedId)
        {
            var follow = await _unitOfWork.Follows.GetFollowRecord(followerId, followedId);
            return _mapper.Map<FollowResponse?>(follow);
        }
    }
}
