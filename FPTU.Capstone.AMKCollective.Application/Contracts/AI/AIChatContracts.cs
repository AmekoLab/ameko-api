using System;
using System.Collections.Generic;

namespace FPTU.Capstone.AMKCollective.Application.Contracts.AI
{
    // ──────────────────────────────────────────────
    // REQUEST
    // ──────────────────────────────────────────────

    public class AIChatRequestDTO
    {
        /// <summary>
        /// ID của conversation cũ để tiếp tục hội thoại.
        /// Null = tạo conversation mới.
        /// </summary>
        public int? ConversationId { get; set; }

        /// <summary>Câu hỏi / yêu cầu của user.</summary>
        public string Message { get; set; } = string.Empty;
    }

    // ──────────────────────────────────────────────
    // RESPONSE
    // ──────────────────────────────────────────────

    public class AIChatResponseDTO
    {
        /// <summary>
        /// ID của conversation (mới hoặc cũ).
        /// FE lưu lại để gửi tin tiếp theo trong cùng session.
        /// </summary>
        public int ConversationId { get; set; }

        /// <summary>Câu trả lời text của AI (phần Reasoning).</summary>
        public string Reply { get; set; } = string.Empty;

        /// <summary>Danh sách sản phẩm đề xuất kèm ảnh, giá, link shop.</summary>
        public List<AIRecommendationItemDTO> Items { get; set; } = new();

        /// <summary>
        /// Nguồn tham khảo từ web search (chỉ có khi Qdrant không tìm được kết quả phù hợp).
        /// FE dùng để hiển thị ảnh + link bàn phím tham khảo trước khi user tạo commission request.
        /// </summary>
        public List<WebSearchSourceLink> SourceLinks { get; set; } = new();

        /// <summary>True khi AI dùng web search thay vì DB inventory.</summary>
        public bool UsedWebSearch { get; set; }

        /// <summary>Giá ước tính (VND) từ AI — có giá trị cả khi dùng web search.</summary>
        public decimal EstimatedPrice { get; set; }
    }

    // ──────────────────────────────────────────────
    // WEB SEARCH SOURCE LINKS
    // ──────────────────────────────────────────────

    public class WebSearchSourceLink
    {
        /// <summary>Tiêu đề trang web / bài viết.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>URL nguồn (GeekHack, Reddit, YouTube, etc.).</summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>Ảnh bàn phím tham khảo lấy từ kết quả web search.</summary>
        public string? ImageUrl { get; set; }

        /// <summary>Đoạn trích nội dung ngắn (200 ký tự).</summary>
        public string? Snippet { get; set; }
    }

    // ──────────────────────────────────────────────
    // CONVERSATION LIST
    // ──────────────────────────────────────────────

    public class AIChatConversationSummaryDTO
    {
        public int Id { get; set; }

        /// <summary>Tiêu đề tự sinh từ câu hỏi đầu tiên.</summary>
        public string? Title { get; set; }

        /// <summary>Nội dung tin nhắn cuối cùng (preview).</summary>
        public string? LastMessage { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    // ──────────────────────────────────────────────
    // MESSAGE LIST
    // ──────────────────────────────────────────────

    public class AIChatMessageDTO
    {
        public int Id { get; set; }

        /// <summary>"user" hoặc "assistant"</summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>Nội dung text của tin nhắn.</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Sản phẩm đề xuất — chỉ có khi Role = "assistant" và AI trả về items.
        /// Null với user messages hoặc assistant messages không có recommendation.
        /// </summary>
        public List<AIRecommendationItemDTO>? Items { get; set; }

        /// <summary>
        /// Nguồn tham khảo web search — chỉ có khi assistant message dùng web search.
        /// </summary>
        public List<WebSearchSourceLink>? SourceLinks { get; set; }

        public bool UsedWebSearch { get; set; }

        public decimal EstimatedPrice { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
