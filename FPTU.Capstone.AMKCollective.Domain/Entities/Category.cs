using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class Category : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ThumbnailURL { get; set; }
        public Guid? ParentId { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid? ShopId { get; set; }

        // Navigation Properties
        public virtual ShopProfile? Shop { get; set; }
        public virtual Category? Parent { get; set; }
        public virtual ICollection<Category> SubCategories { get; set; } = new List<Category>();
        public virtual ICollection<Model> Models { get; set; } = new List<Model>();
    }
}
