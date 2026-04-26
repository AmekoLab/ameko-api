using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    /// <summary>
    /// Một tin nhắn trong phiên hội thoại AI chatbot.
    /// Role = "user" | "assistant".
    /// Dùng int PK vì high-volume — cùng pattern với Message.
    /// </summary>
    public class AIChatMessage : BaseEntityInt
    {
        public int ConversationId { get; set; }

        /// <summary>"user" hoặc "assistant"</summary>
        public string Role { get; set; } = null!;

        /// <summary>
        /// Nội dung text thuần của tin nhắn.
        /// Với assistant: đây là phần Reasoning (giải thích) mà LLM sinh ra.
        /// </summary>
        public string Content { get; set; } = null!;

        /// <summary>
        /// JSON serialized của AIRecommendationResponseDTO (chỉ có ở assistant messages).
        /// Lưu riêng để FE có thể render product cards mà không cần parse toàn bộ Content.
        /// </summary>
        public string? RecommendationPayload { get; set; }

        // Navigation
        public virtual AIChatConversation Conversation { get; set; } = null!;
    }
}
