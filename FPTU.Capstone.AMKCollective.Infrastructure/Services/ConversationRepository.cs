using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class ConversationRepository : IConversationRepository
    {
        private readonly ApplicationDbContext _context;

        public ConversationRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Conversation?> GetConversationByIdAsync(int conversationId)
        {
            return await _context.Conversations.FindAsync(conversationId);
        }

        public async Task<Conversation?> GetConversationByUserIdsAsync(Guid userOneId, Guid userTwoId)
        {
            return await _context.Conversations
                .Where(c => _context.UserConversations.Count(uc => uc.ConversationId == c.Id) == 2)
                .Where(c => _context.UserConversations.Any(uc => uc.ConversationId == c.Id && uc.UserId == userOneId))
                .Where(c => _context.UserConversations.Any(uc => uc.ConversationId == c.Id && uc.UserId == userTwoId))
                .FirstOrDefaultAsync();
        }

        public async Task AddConversationAsync(Conversation conversation)
        {
            await _context.Conversations.AddAsync(conversation);
        }

        public async Task AddMessageAsync(Message message)
        {
            await _context.Messages.AddAsync(message);
        }

        public async Task<List<Message>> GetMessagesByConversationIdAsync(int conversationId)
        {
            return await _context.MessageRecipients
                .Where(mr => mr.UserConversation.ConversationId == conversationId)
                .Select(mr => mr.Message)
                .Distinct()
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();
        }

        public void UpdateConversation(Conversation conversation)
        {
            _context.Conversations.Update(conversation);
        }

        public void DeleteConversation(Conversation conversation)
        {
            _context.Conversations.Remove(conversation);
        }
    }
}
