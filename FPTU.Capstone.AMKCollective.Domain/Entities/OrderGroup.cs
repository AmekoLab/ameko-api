using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class OrderGroup : BaseEntity
    {
        public Guid CustomerId { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalGroupAmount { get; set; }
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
        // Navigation Properties
        [ForeignKey("CustomerId")]
        public virtual User Customer { get; set; } = null!;
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>(0);
        public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}
