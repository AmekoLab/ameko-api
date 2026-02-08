using System;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Follow
{
    /// <summary>
    /// Request DTO for checking follow relationship
    /// Used by POST /api/v1/follows/check
    /// 
    /// FE INSTRUCTION:
    /// Send the ID of the user you want to check if YOU are following them
    /// </summary>
    public class CheckFollowingRequestDto
    {
        /// <summary>
        /// ID of the user you want to check if you're following
        /// </summary>
        public Guid UserId { get; set; }
    }
}
