using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class OrderGroup : BaseEntity
    {
        public Guid? WalletId { get; set; }
        public decimal Amount { get; set; }
        public decimal BalanceBefore { get; set; }

        // Navigation Properties
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
        public virtual Payment? Payment { get; set; }
    }
}
