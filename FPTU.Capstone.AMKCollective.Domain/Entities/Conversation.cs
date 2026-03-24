using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class Conversation : BaseEntityInt
    {
        public string? Name { get; set; }
        public string? Image { get; set; }
        public bool IsRestricted { get; set; } = false;

        // Relationship
        public virtual ICollection<UserConversation> UserConversations { get; set; } =
            new List<UserConversation>();
    }
}
