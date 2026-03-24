using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IMessageRepository
    {
        Task<Message?> GetMessageByIdAsync(int messageId);
        Task AddMessageAsync(Message message);
        Task<List<Message>> GetMessagesByConversationIdAsync(int conversationId);
        void UpdateMessage(Message message);
        void DeleteMessage(Message message);
    }
}
