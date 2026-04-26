using System;
using System.Collections.Generic;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    /// <summary>
    /// Một phiên hội thoại giữa user và AI chatbot tư vấn bàn phím cơ.
    /// Dùng int PK vì high-volume, internal — cùng pattern với Conversation và Message.
    /// </summary>
    public class AIChatConversation : BaseEntityInt
    {
        public Guid UserId { get; set; }

        /// <summary>
        /// Tiêu đề tự động sinh từ câu hỏi đầu tiên của user (truncate 60 ký tự).
        /// Null khi conversation mới tạo, được điền sau khi LLM trả về lần đầu.
        /// </summary>
        public string? Title { get; set; }

        // Navigation
        public virtual User User { get; set; } = null!;
        public virtual ICollection<AIChatMessage> Messages { get; set; } = new List<AIChatMessage>();
    }
}
