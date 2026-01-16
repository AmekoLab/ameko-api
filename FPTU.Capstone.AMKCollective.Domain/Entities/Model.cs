using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class Model : BaseEntity
    {
        public Guid ShopId { get; set; }
        public Guid CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ThumbnailURL { get; set; }
        public string? PartType { get; set; }
        public string? Description { get; set; }
        public int StockQuantity { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation Properties
        public virtual ShopProfile Shop { get; set; } = null!;
        public virtual Category Category { get; set; } = null!;
        public virtual ICollection<KitDesignOption> KitDesignOptions { get; set; } = new List<KitDesignOption>();
        public virtual ICollection<ProductAssembledDetail> ProductAssembledDetails { get; set; } = new List<ProductAssembledDetail>();
    }
}
