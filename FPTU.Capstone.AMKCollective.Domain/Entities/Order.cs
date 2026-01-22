using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class Order : BaseEntity
    {
        public Guid? OrderGroupId { get; set; }
        public Guid? VoucherId { get; set; }
        public Guid CustomerId { get; set; }
        public Guid ShopId { get; set; }
        [Required]
        [MaxLength(100)]
        public string ReceiverName { get; set; } = string.Empty;
        [Required]
        [MaxLength(20)]
        public string ReceiverPhone { get; set; } = string.Empty;
        [Required]
        [MaxLength(500)]
        public string ShippingAddress { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal ShippingFee { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; } = 0;

        public string OrderStatus { get; set; } = "Pending"; 
        public string PaymentStatus { get; set; } = "Pending";

        public string? Note { get; set; } 
        public string? CancelReason { get; set; }

        // Navigation Properties
        public virtual OrderGroup? OrderGroup { get; set; }
        public virtual Voucher? Voucher { get; set; }
        public virtual User Customer { get; set; } = null!;
        public virtual ShopProfile Shop { get; set; } = null!;
        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
    }
}
