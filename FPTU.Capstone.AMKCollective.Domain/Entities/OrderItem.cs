using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class OrderItem : BaseEntity
    {
        public Guid OrderId { get; set; }
        public Guid? AssembledProductId { get; set; }
        public Guid? ProductId { get; set; }
        // Snapshot data
        public string ProductName { get; set; } = string.Empty;
        public string ProductImage { get; set; } = string.Empty;
        //quantity and price
        public int Quantity { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; } = 0;
        //Custom
        public bool IsCustom { get; set; } = false;
        public string? DesignConfig { get; set; }
        public string? Notes { get; set; }




        //public decimal DiscountAmount { get; set; }
        //public string? ItemStatus { get; set; }

        // Navigation Properties
        public virtual AssembledProduct? AssembledProduct { get; set; }
        public virtual Model? Product { get; set; }
        public virtual Order Order { get; set; } = null!;
        public virtual ICollection<OrderItemComponent> OrderItemComponents { get; set; } = new List<OrderItemComponent>();
    }
}
