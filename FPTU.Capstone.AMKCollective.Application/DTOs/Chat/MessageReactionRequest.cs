using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Chat
{
    /// <summary>
    /// Message reaction request.
    /// </summary>
    public class MessageReactionRequest
    {
        /// <summary>
        /// Reaction enum value. Use null to remove reaction.
        /// </summary>
        public MessageReaction? Reaction { get; set; }
    }
}
