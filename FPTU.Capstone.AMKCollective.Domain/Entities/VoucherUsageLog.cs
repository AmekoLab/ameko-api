using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class VoucherUsageLog
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid VoucherId { get; set; }
        public string Code { get; set; } = string.Empty;

        // Navigation Properties
        public virtual User User { get; set; } = null!;
        public virtual Voucher Voucher { get; set; } = null!;

        public VoucherUsageLog()
        {
            Id = Guid.NewGuid();
        }
    }
}
