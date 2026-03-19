using System;
using FPTU.Capstone.AMKCollective.Domain.Enums;

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
        public FPTU.Capstone.AMKCollective.Domain.Enums.NotificationType Type { get; set; }
        public bool IsRead { get; set; } = false;

        // Social Commerce Extensions
        public Guid? ActorId { get; set; } 
        
        // Polymorphic Reference Fields
        public string? ReferenceId { get; set; }
        public string? ReferenceType { get; set; }
        public string? RedirectUrl { get; set; }

        // Navigation Properties
        public virtual User User { get; set; } = null!;
    }
}
