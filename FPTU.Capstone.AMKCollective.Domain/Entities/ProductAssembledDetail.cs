using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class ProductAssembledDetail
    {
        public Guid Id { get; set; }
        public Guid AssembledProductId { get; set; }
        public Guid BaseKitId { get; set; }
        public Guid ComponentId { get; set; }
        public int Quantity { get; set; }

        // Navigation Properties
        public virtual AssembledProduct AssembledProduct { get; set; } = null!;
        public virtual Model BaseKit { get; set; } = null!;
        public virtual Model Component { get; set; } = null!;

        public ProductAssembledDetail()
        {
            Id = Guid.NewGuid();
        }
    }
}
