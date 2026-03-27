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

        public async Task<UserConversation?> GetUserConversationAsync(Guid userId, int conversationId)
        {
            return await _context.UserConversations
                .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.ConversationId == conversationId);
        }

        public async Task<List<UserConversation>> GetConversationParticipantsAsync(int conversationId)
        {
            return await _context.UserConversations
                .Where(uc => uc.ConversationId == conversationId)
                .ToListAsync();
        }

        public async Task<List<UserConversation>> GetUserConversationsAsync(Guid userId)
        {
            return await _context.UserConversations
                .Where(uc => uc.UserId == userId)
                .Include(uc => uc.Conversation)
                .ToListAsync();
        }

        public async Task AddConversationAsync(Conversation conversation)
        {
            await _context.Conversations.AddAsync(conversation);
        }

        public async Task AddUserConversationAsync(UserConversation userConversation)
        {
            await _context.UserConversations.AddAsync(userConversation);
        }

        public async Task AddMessageAsync(Message message)
        {
            await _context.Messages.AddAsync(message);
        }

        public async Task<Message?> GetMessageByIdAsync(int messageId)
        {
            return await _context.Messages.FindAsync(messageId);
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

        public async Task<bool> IsMessageInConversationAsync(int messageId, int conversationId)
        {
            return await _context.MessageRecipients
                .AnyAsync(mr => mr.MessageId == messageId && mr.UserConversation.ConversationId == conversationId);
        }

        public async Task<MessageRecipient?> GetMessageRecipientAsync(Guid userId, int conversationId, int messageId)
        {
            return await _context.MessageRecipients
                .FirstOrDefaultAsync(mr => mr.UserId == userId
                                           && mr.MessageId == messageId
                                           && mr.UserConversation.ConversationId == conversationId);
        }

        public async Task<List<MessageRecipient>> GetUnreadRecipientsAsync(Guid userId, int conversationId, int upToMessageId)
        {
            return await _context.MessageRecipients
                .Where(mr => mr.UserId == userId
                             && !mr.IsRead
                             && mr.MessageId <= upToMessageId
                             && mr.UserConversation.ConversationId == conversationId)
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(Guid userId, int conversationId)
        {
            return await _context.MessageRecipients
                .Where(mr => mr.UserId == userId
                             && !mr.IsRead
                             && mr.UserConversation.ConversationId == conversationId)
                .CountAsync();
        }

        public async Task AddMessageRecipientsAsync(IEnumerable<MessageRecipient> recipients)
        {
            await _context.MessageRecipients.AddRangeAsync(recipients);
        }

        public void UpdateConversation(Conversation conversation)
        {
            _context.Conversations.Update(conversation);
        }

        public void UpdateMessageRecipient(MessageRecipient recipient)
        {
            _context.MessageRecipients.Update(recipient);
        }

        public void UpdateMessageRecipients(IEnumerable<MessageRecipient> recipients)
        {
            _context.MessageRecipients.UpdateRange(recipients);
        }

        public void DeleteConversation(Conversation conversation)
        {
            _context.Conversations.Remove(conversation);
        }
    }
}
