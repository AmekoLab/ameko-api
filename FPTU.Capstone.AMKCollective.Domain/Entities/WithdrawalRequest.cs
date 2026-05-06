using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class WithdrawalRequest : BaseEntity
    {
        public Guid UserId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal FeeAmount { get; set; }

        [MaxLength(255)]
        public string BankName { get; set; } = null!;

        [MaxLength(255)]
        public string BankAccountNumber { get; set; } = null!;

        [MaxLength(255)]
        public string BankAccountName { get; set; } = null!;

        public WithdrawalStatus Status { get; set; } = WithdrawalStatus.Pending;

        public Guid? AdminId { get; set; }
        
        [MaxLength(1000)]
        public string? AdminMessage { get; set; }

        [MaxLength(1000)]
        public string? EvidenceUrl { get; set; }

        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }

        // Navigation properties
        public virtual User? User { get; set; }
        public virtual User? Admin { get; set; }
    }
}
