namespace FPTU.Capstone.AMKCollective.Application.DTOs.Follow
{
    /// <summary>
    /// Response DTO representing a user that is FOLLOWING the current user
    /// 
    /// Example:
    /// If User A follows User B, when User B calls GET /api/v1/follows/followers/{userBId},
    /// they will see User A in the response with this DTO structure.
    /// UserId = A's ID
    /// </summary>
    public class FollowerResponse
    {
        /// <summary>
        /// ID of the user who is following (FollowerId in database)
        /// This is a user that is following YOUR account
        /// </summary>
        public Guid UserId { get; set; }
    }
}
