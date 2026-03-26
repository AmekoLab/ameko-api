namespace FPTU.Capstone.AMKCollective.Application.DTOs.Chat
{
    public class ConversationResponse
    {
        public int ConversationId { get; set; }
        public Guid OtherUserId { get; set; }
        public string OtherUserName { get; set; } = string.Empty;
        public string? OtherUserAvatarUrl { get; set; }
        public string LastMessage { get; set; } = string.Empty;
        public DateTime? LastMessageAt { get; set; }
        public int UnreadCount { get; set; }
    }
}
