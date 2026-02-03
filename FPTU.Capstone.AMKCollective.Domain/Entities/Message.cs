using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    /// <summary>
    /// Message entity with int PK for better performance (high-volume chat messages).
    /// </summary>
    public class Message : BaseEntityInt
    {
        public Guid ConversationId { get; set; } // FK to Conversation (Guid)
        public Guid SenderId { get; set; }       // FK to User (Guid)
        public string Content { get; set; } = string.Empty;
        public bool IsRead { get; set; } = false;

        // Navigation Properties
        public virtual Conversation Conversation { get; set; } = null!;
        public virtual User Sender { get; set; } = null!;
    }
}
