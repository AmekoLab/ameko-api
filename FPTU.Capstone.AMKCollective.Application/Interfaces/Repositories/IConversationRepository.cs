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
        Task AddConversationAsync(Conversation conversation);
        Task AddMessageAsync(Message message);
        Task<List<Message>> GetMessagesByConversationIdAsync(int conversationId);
        void UpdateConversation(Conversation conversation);
        void DeleteConversation(Conversation conversation);
    }
}
