using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class OrderItemComponent : BaseEntity
    {
        public Guid OrderItemId { get; set; }
        public Guid PartId { get; set; }
        public decimal PartPriceSnapshot { get; set; }
        public string? PartName { get; set; }
        public string? PartImageUrl { get; set; }
        public int Quantity { get; set; }
        public string? Notes { get; set; }

        // Navigation Properties
        public virtual OrderItem OrderItem { get; set; } = null!;
        public virtual Model Part { get; set; } = null!;
    }
}
