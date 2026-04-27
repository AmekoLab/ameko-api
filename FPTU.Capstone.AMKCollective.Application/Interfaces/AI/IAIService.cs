using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Application.Contracts.AI;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.AI
{
    public interface IAIService
    {
        /// <summary>
        /// Generates a personalized keyboard build recommendation based on user input and context.
        /// </summary>
        Task<AIRecommendationResponseDTO> GetRecommendationAsync(AIRecommendationRequestDTO request);

        // ── AI Chatbot (stateful multi-turn) ──────────────────────────────────

        /// <summary>
        /// Gửi tin nhắn tới AI chatbot. Tạo conversation mới nếu ConversationId = null,
        /// tiếp tục conversation cũ nếu ConversationId có giá trị.
        /// LLM nhận toàn bộ lịch sử hội thoại làm context.
        /// </summary>
        Task<AIChatResponseDTO> ChatAsync(Guid userId, AIChatRequestDTO request);

        /// <summary>
        /// Streaming variant: yield chunks qua Server-Sent Events.
        /// Logic AI giống ChatAsync 100%, chỉ khác cách trả response (chunked thay vì 1 cục).
        /// FE giữ connection alive nhờ heartbeats → không bị timeout dù xử lý lâu.
        /// </summary>
        IAsyncEnumerable<ChatChunk> ChatStreamAsync(Guid userId, AIChatRequestDTO request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Danh sách các phiên hội thoại AI của user, mới nhất trước.
        /// </summary>
        Task<List<AIChatConversationSummaryDTO>> GetChatConversationsAsync(Guid userId, int limit = 20);

        /// <summary>
        /// Toàn bộ tin nhắn trong một phiên hội thoại AI theo thứ tự thời gian.
        /// </summary>
        Task<List<AIChatMessageDTO>> GetChatMessagesAsync(Guid userId, int conversationId);

        /// <summary>
        /// Performs a semantic search for shops.
        /// </summary>
        Task<IEnumerable<FPTU.Capstone.AMKCollective.Application.DTOs.Shop.ShopResponse>> SearchShopsAsync(string query, int limit = 10);

        /// <summary>
        /// Performs a semantic search for assembled products (builds).
        /// </summary>
        Task<IEnumerable<FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProduct.AssembledProductResponse>> SearchBuildsAsync(string query, int limit = 10);

        /// <summary>
        /// Synchronizes all eligible entities to Qdrant (Admin tool).
        /// </summary>
        Task SyncAllEntitiesToQdrantAsync();

        Task SyncShopAsync(ShopProfile shop);
        Task SyncBuildAsync(AssembledProduct build);
        Task SyncPartAsync(Model part);

        /// <summary>
        /// Analyzes an order issue (cancellation/refund) and provides an AI recommendation.
        /// </summary>
        Task<string?> AnalyzeOrderIssueAsync(OrderIssue issue, Order order);
    }
}
