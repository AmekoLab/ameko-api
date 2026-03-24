using System;
using FPTU.Capstone.AMKCollective.Domain.Enums;
namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    /// <summary>
    /// Message entity with int PK for better performance (high-volume chat messages).
    /// </summary>
    public class Message : BaseEntityInt
    {
        public string Content { get; set; } = null!;
        public string? AttachmentUrl { get; set; }
        public MediaType MessageType { get; set; } = MediaType.Text;
        public bool IsPinned { get; set; } = false;

        // Foreign key
        public Guid SenderId { get; set; }
        public int? ParentMessageId { get; set; }
        //public Guid? OfferId { get; set; }

        // Relationship
        public User Sender { get; set; } = null!;
        public Message? ParentMessage { get; set; }
        public virtual ICollection<Message> Replies { get; set; } = new List<Message>();
        public virtual ICollection<MessageRecipient> MessageRecipients { get; set; } = new List<MessageRecipient>();
    }
}
