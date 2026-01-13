using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class Notification
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public bool IsRead { get; set; } = false;

        // Navigation Properties
        public virtual User User { get; set; } = null!;

        public Notification()
        {
            Id = Guid.NewGuid();
        }
    }
}
