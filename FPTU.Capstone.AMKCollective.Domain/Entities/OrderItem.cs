using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class OrderItem
    {
        public Guid Id { get; set; }
        public Guid AssembledProductId { get; set; }
        public Guid OrderId { get; set; }
        public string? DesignConfig { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? ItemStatus { get; set; }
        public string? Notes { get; set; }
        public string? ItemType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation Properties
        public virtual AssembledProduct AssembledProduct { get; set; } = null!;
        public virtual Order Order { get; set; } = null!;
        public virtual ICollection<OrderItemComponent> OrderItemComponents { get; set; } = new List<OrderItemComponent>();

        public OrderItem()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }
    }
}
