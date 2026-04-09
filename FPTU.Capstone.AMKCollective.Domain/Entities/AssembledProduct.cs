using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class AssembledProduct : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? View3DUrl { get; set; }
        public decimal Price { get; set; }

        // Optional Fields
        public string? Image1 { get; set; }
        public string? Image2 { get; set; }
        public string? Image3 { get; set; }
        public string? Description { get; set; }
        public int? Quantity { get; set; }

        // Specification Fields
        public string? Layout { get; set; }
        public string? Mounting { get; set; }
        public string? PCB { get; set; }
        public string? Connection { get; set; }
        public string? Battery { get; set; }

        public double Rating { get; set; } = 0;
        public int TotalReviews { get; set; } = 0;

        /// <summary>
        /// JSON-serialized vector embedding for semantic search.
        /// </summary>
        public string? Embedding { get; set; }

        // Navigation Properties
        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public virtual ICollection<ProductAssembledDetail> ProductAssembledDetails { get; set; } = new List<ProductAssembledDetail>();
        public virtual ICollection<AssembledProductFeedback> Feedbacks { get; set; } = new List<AssembledProductFeedback>();
    }
}
