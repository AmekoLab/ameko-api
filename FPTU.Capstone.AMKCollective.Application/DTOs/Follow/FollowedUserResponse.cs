using System;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Follow
{
    /// <summary>
    /// Response DTO representing a user that the current user is FOLLOWING
    /// 
    /// Example:
    /// If User A follows User B, when User A calls GET /api/v1/follows,
    /// they will see User B in the response with this DTO structure.
    /// UserId = B's ID
    /// </summary>
    public class FollowedUserResponse
    {
        /// <summary>
        /// ID of the user being followed (FollowedId in database)
        /// This is the user that YOUR account is following
        /// </summary>
        public Guid UserId { get; set; }
    }
}
