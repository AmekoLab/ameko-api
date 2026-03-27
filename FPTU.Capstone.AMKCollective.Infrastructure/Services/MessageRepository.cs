using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    // Triển khai repository cho Message nếu cần
    public class MessageRepository : FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories.IMessageRepository
    {
        private readonly ApplicationDbContext _context;

        public MessageRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Message?> GetMessageByIdAsync(int messageId)
        {
            return await _context.Messages.FindAsync(messageId);
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

        public void UpdateMessage(Message message)
        {
            _context.Messages.Update(message);
        }

        public void DeleteMessage(Message message)
        {
            _context.Messages.Remove(message);
        }
    }
}
