namespace FPTU.Capstone.AMKCollective.Domain.Entities;

public class UserConversation : BaseEntityInt
{
    public bool IsArchived { get; set; } = false;
    public bool IsOwner { get; set; } = false;

    // Foreign key
    public Guid UserId { get; set; }
    public int ConversationId { get; set; }

    // Relationship
    public User User { get; set; } = null!;
    public Conversation Conversation { get; set; } = null!;
    public virtual ICollection<MessageRecipient> MessageRecipients { get; set; } = new List<MessageRecipient>();
}