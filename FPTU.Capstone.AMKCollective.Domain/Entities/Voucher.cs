using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class Voucher
    {
        public Guid Id { get; set; }
        public Guid CreatorId { get; set; }
        public string Code { get; set; } = string.Empty;

        // Navigation Properties
        public virtual User Creator { get; set; } = null!;
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
        public virtual ICollection<VoucherUsageLog> VoucherUsageLogs { get; set; } = new List<VoucherUsageLog>();

        public Voucher()
        {
            Id = Guid.NewGuid();
        }
    }
}
