using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class OrderItem : BaseEntity
    {
        public Guid? AssembledProductId { get; set; }
        public Guid? ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductImage { get; set; } = string.Empty;
        public bool IsCustom { get; set; } = false;
        public Guid OrderId { get; set; }
        public string? DesignConfig { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? ItemStatus { get; set; }
        public string? Notes { get; set; }

        // Navigation Properties
        public virtual AssembledProduct? AssembledProduct { get; set; }
        public virtual Model? Product { get; set; }
        public virtual Order Order { get; set; } = null!;
        public virtual ICollection<OrderItemComponent> OrderItemComponents { get; set; } = new List<OrderItemComponent>();
    }
}
