using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class AIChatRepository : IAIChatRepository
    {
        private readonly ApplicationDbContext _context;

        public AIChatRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AIChatConversation?> GetConversationByIdAsync(int id, Guid userId)
        {
            return await _context.AIChatConversations
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && !c.IsDeleted);
        }

        public async Task<List<AIChatConversation>> GetConversationsByUserAsync(Guid userId, int limit = 20)
        {
            // Lấy conversation kèm tin nhắn cuối để hiển thị preview
            // Include filtered: chỉ lấy 1 message gần nhất (không soft-deleted)
            return await _context.AIChatConversations
                .Where(c => c.UserId == userId && !c.IsDeleted)
                .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
                .Take(limit)
                .Include(c => c.Messages
                    .Where(m => !m.IsDeleted)
                    .OrderByDescending(m => m.Id)
                    .Take(1))
                .ToListAsync();
        }

        public async Task AddConversationAsync(AIChatConversation conversation)
        {
            await _context.AIChatConversations.AddAsync(conversation);
        }

        public async Task AddMessageAsync(AIChatMessage message)
        {
            await _context.AIChatMessages.AddAsync(message);
        }

        public async Task<List<AIChatMessage>> GetRecentMessagesAsync(int conversationId, int limit = 10)
        {
            // Lấy N tin gần nhất rồi đảo chiều → thứ tự cũ→mới để đưa vào LLM context
            var messages = await _context.AIChatMessages
                .Where(m => m.ConversationId == conversationId && !m.IsDeleted)
                .OrderByDescending(m => m.Id)
                .Take(limit)
                .ToListAsync();

            messages.Reverse();
            return messages;
        }

        public async Task<List<AIChatMessage>> GetAllMessagesAsync(int conversationId)
        {
            return await _context.AIChatMessages
                .Where(m => m.ConversationId == conversationId && !m.IsDeleted)
                .OrderBy(m => m.Id)
                .ToListAsync();
        }

        public void UpdateConversation(AIChatConversation conversation)
        {
            _context.AIChatConversations.Update(conversation);
        }
    }
}
