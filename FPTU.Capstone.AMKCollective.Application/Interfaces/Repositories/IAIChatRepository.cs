using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IAIChatRepository
    {
        /// <summary>Load conversation theo id, kiểm tra ownership theo userId.</summary>
        Task<AIChatConversation?> GetConversationByIdAsync(int id, Guid userId);

        /// <summary>Danh sách conversations của user, mới nhất trước.</summary>
        Task<List<AIChatConversation>> GetConversationsByUserAsync(Guid userId, int limit = 20);

        Task AddConversationAsync(AIChatConversation conversation);

        Task AddMessageAsync(AIChatMessage message);

        /// <summary>
        /// Lấy N tin nhắn gần nhất của conversation theo thứ tự thời gian (cũ → mới).
        /// Dùng để build context history cho LLM.
        /// </summary>
        Task<List<AIChatMessage>> GetRecentMessagesAsync(int conversationId, int limit = 10);

        /// <summary>Lấy toàn bộ messages theo thứ tự thời gian để hiển thị trên FE.</summary>
        Task<List<AIChatMessage>> GetAllMessagesAsync(int conversationId);

        void UpdateConversation(AIChatConversation conversation);
    }
}
