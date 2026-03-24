using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Domain.Entities;

public class MessageRecipient : BaseEntityInt
{
    public MessageReaction? MessageReaction { get; set; }
    public bool IsRead { get; set; } = false;

    // Foreign key
    public Guid UserId { get; set; }
    public int UserConversationId { get; set; }
    public int MessageId { get; set; }

    // Relationship
    public User User { get; set; } = null!;
    public UserConversation UserConversation { get; set; } = null!;
    public Message Message { get; set; } = null!;
}