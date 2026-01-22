using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class OrderGroup : BaseEntity
    {
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalGroupAmount { get; set; }
        public string PaymentStatus { get; set; } = "Pending";
        // Navigation Properties
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>(0);
    }
}
