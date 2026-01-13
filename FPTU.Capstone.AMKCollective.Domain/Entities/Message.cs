using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class Message
    {
        public Guid Id { get; set; }
        public Guid ConversationId { get; set; }
        public Guid SenderId { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsRead { get; set; } = false;

        // Navigation Properties
        public virtual Conversation Conversation { get; set; } = null!;
        public virtual User Sender { get; set; } = null!;

        public Message()
        {
            Id = Guid.NewGuid();
        }
    }
}
