using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    /// <summary>
    /// Notification entity with int PK for better performance (high-volume inserts).
    /// </summary>
    public class Notification : BaseEntityInt
    {
        public Guid UserId { get; set; } // FK to User (Guid)
        public string Title { get; set; } = string.Empty;
        public string? Message { get; set; }
        public string? Type { get; set; }
        public bool IsRead { get; set; } = false;

        // Navigation Properties
        public virtual User User { get; set; } = null!;
    }
}
