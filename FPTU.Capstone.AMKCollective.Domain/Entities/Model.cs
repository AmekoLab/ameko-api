using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class Model : BaseEntity
    {
        public Guid ShopId { get; set; }
        public Guid CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ThumbnailURL { get; set; }
        public string? DefaultLayerImageUrl { get; set; }
        public string? Specifications { get; set; } // JSON
        public string? PartType { get; set; }
        public decimal Price { get; set; }
        public string? Description { get; set; }
        public int StockQuantity { get; set; }
        public bool IsActive { get; set; } = true;

        /// True = part này có thể dùng để custom per-key trên bàn phím ảo (add-on)
        public bool IsAddonEligible { get; set; } = false;

        /// True = tất cả bước trong workflow đều đã có ít nhất 1 KitDesignOption. Được tự động tính lại bởi service.
        public bool IsBuilderReady { get; set; } = false;

        /// <summary>
        /// JSON-serialized vector embedding for semantic search.
        /// </summary>
        public string? Embedding { get; set; }

        // Navigation Properties
        public virtual ShopProfile Shop { get; set; } = null!;
        public virtual Category Category { get; set; } = null!;
        [InverseProperty("BaseKit")]
        public virtual ICollection<KitDesignOption> AsBaseKitOptions { get; set; } = new List<KitDesignOption>();

        [InverseProperty("Component")]
        public virtual ICollection<KitDesignOption> AsComponentOptions { get; set; } = new List<KitDesignOption>();
        public virtual ICollection<ProductAssembledDetail> ProductAssembledDetails { get; set; } = new List<ProductAssembledDetail>();
    }
}
