using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IConversationRepository
    {
        Task<Conversation?> GetConversationByIdAsync(int conversationId);
        Task<Conversation?> GetConversationByUserIdsAsync(Guid userOneId, Guid userTwoId);
        Task<UserConversation?> GetUserConversationAsync(Guid userId, int conversationId);
        Task<List<UserConversation>> GetConversationParticipantsAsync(int conversationId);
        Task<List<UserConversation>> GetUserConversationsAsync(Guid userId);
        Task AddConversationAsync(Conversation conversation);
        Task AddUserConversationAsync(UserConversation userConversation);
        Task AddMessageAsync(Message message);
        Task<Message?> GetMessageByIdAsync(int messageId);
        Task<List<Message>> GetMessagesByConversationIdAsync(int conversationId);
        Task<bool> IsMessageInConversationAsync(int messageId, int conversationId);
        Task<MessageRecipient?> GetMessageRecipientAsync(Guid userId, int conversationId, int messageId);
        Task<List<MessageRecipient>> GetUnreadRecipientsAsync(Guid userId, int conversationId, int upToMessageId);
        Task<int> GetUnreadCountAsync(Guid userId, int conversationId);
        Task AddMessageRecipientsAsync(IEnumerable<MessageRecipient> recipients);
        void UpdateConversation(Conversation conversation);
        void UpdateMessageRecipient(MessageRecipient recipient);
        void UpdateMessageRecipients(IEnumerable<MessageRecipient> recipients);
        void DeleteConversation(Conversation conversation);
    }
}
