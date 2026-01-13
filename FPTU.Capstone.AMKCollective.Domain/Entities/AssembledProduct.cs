using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class AssembledProduct
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? View3DUrl { get; set; }
        public decimal Price { get; set; }

        // Navigation Properties
        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public virtual ICollection<ProductAssembledDetail> ProductAssembledDetails { get; set; } = new List<ProductAssembledDetail>();

        public AssembledProduct()
        {
            Id = Guid.NewGuid();
        }
    }
}
