using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class Feedback
    {
        public Guid Id { get; set; }
        public Guid OrderId { get; set; }
        public Guid FromUserId { get; set; }
        public Guid ToUserId { get; set; }
        public string? Location { get; set; }

        // Navigation Properties
        public virtual Order Order { get; set; } = null!;
        public virtual User FromUser { get; set; } = null!;
        public virtual User ToUser { get; set; } = null!;

        public Feedback()
        {
            Id = Guid.NewGuid();
        }
    }
}
