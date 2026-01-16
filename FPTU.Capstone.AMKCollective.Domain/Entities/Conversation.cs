using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class Conversation : BaseEntity
    {
        public Guid UserOneId { get; set; }
        public Guid UserTwoId { get; set; }

        // Navigation Properties
        public virtual User UserOne { get; set; } = null!;
        public virtual User UserTwo { get; set; } = null!;
        public virtual ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}
