using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class OrderGroup : BaseEntity
    {
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalGroupAmount { get; set; }
        public string PaymentStatus { get; set; } = "Pending";
        public string? StripeSessionId { get; set; }
        public string? StripePaymentIntentId { get; set; }
        // Navigation Properties
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
        public virtual Payment? Payment { get; set; }
    }
}
