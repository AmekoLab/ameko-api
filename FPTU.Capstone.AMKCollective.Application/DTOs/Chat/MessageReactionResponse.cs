using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Chat
{
    public class MessageReactionResponse
    {
        public int ConversationId { get; set; }
        public int MessageId { get; set; }
        public Guid UserId { get; set; }
        public MessageReaction? Reaction { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
