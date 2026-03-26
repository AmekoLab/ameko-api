using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Chat
{
    public class ChatMessageResponse
    {
        public int Id { get; set; }
        public int ConversationId { get; set; }
        public Guid SenderId { get; set; }
        public string Content { get; set; } = string.Empty;
        public MediaType MessageType { get; set; }
        public int? ParentMessageId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
