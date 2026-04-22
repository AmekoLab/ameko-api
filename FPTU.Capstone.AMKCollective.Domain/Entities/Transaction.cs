using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class Transaction : BaseEntity
    {
        public Guid WalletId { get; set; }

        // Dùng khi thanh toán cho cả 1 giỏ hàng (nhiều order) bằng Ví
        public Guid? OrderGroupId { get; set; }

        // Dùng khi hoàn tiền 1 đơn hàng cụ thể, hoặc cộng doanh thu từ 1 đơn hàng cho Shop
        public Guid? RelatedOrderId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; } // Số tiền giao dịch

        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceBeforeTransaction { get; set; } // Số dư ví trước khi giao dịch

        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceAfterTransaction { get; set; } // Số dư ví sau khi giao dịch thành công

        [Column(TypeName = "decimal(18,2)")]
        public decimal HeldBalanceBeforeTransaction { get; set; } // Số dư chờ (Hold) trước khi giao dịch

        [Column(TypeName = "decimal(18,2)")]
        public decimal HeldBalanceAfterTransaction { get; set; } // Số dư chờ (Hold) sau khi giao dịch thành công

        [Column(TypeName = "decimal(18,2)")]
        public decimal FeeAmount { get; set; } = 0; // Số tiền phí bị trừ (Phí rút, phí hoa hồng,...)

        public TransactionDirection Direction { get; set; } // Hướng dòng tiền (In, Out, Held)

        public string Currency { get; set; } = "VND";

        public TransactionType Type { get; set; }
        [Required]
        [MaxLength(50)]
        public string TransactionCode { get; set; } = null!;

        [MaxLength(500)]
        public string? Description { get; set; } // Vd: "Thanh toán đơn hàng #123 bằng số dư ví", "Hoàn tiền đơn hàng #456"
        [MaxLength(100)]
        public string? IdempotencyKey { get; set; }

        // Navigation Properties
        public virtual Wallet? Wallet { get; set; }
        public virtual OrderGroup? OrderGroup { get; set; }
        public virtual Order? RelatedOrder { get; set; }

        // Metadata JSON — stored as json column in DB (Pomelo compatible)
        [Column(TypeName = "json")]
        public string? MetadataJson { get; set; }

        /// <summary>
        /// Typed accessor for MetadataJson — not mapped to a DB column.
        /// </summary>
        [NotMapped]
        public TransactionMetadata? Metadata
        {
            get => MetadataJson == null ? null
                : JsonSerializer.Deserialize<TransactionMetadata>(MetadataJson);
            set => MetadataJson = value == null ? null
                : JsonSerializer.Serialize(value);
        }
    }
}
