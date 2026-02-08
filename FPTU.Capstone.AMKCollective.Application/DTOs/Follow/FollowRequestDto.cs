using System;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Follow
{
    /// <summary>
    /// Request DTO for toggling follow/unfollow
    /// Used by POST /api/v1/follows/toggle
    /// 
    /// FE INSTRUCTION:
    /// Send the ID of the user you want to follow/unfollow
    /// </summary>
    public class FollowRequestDto
    {
        /// <summary>
        /// ID of the user you want to follow or unfollow
        /// </summary>
        public Guid FollowedId { get; set; }
    }
}
