using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class Category
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid? ParentId { get; set; }

        // Navigation Properties
        public virtual Category? Parent { get; set; }
        public virtual ICollection<Category> SubCategories { get; set; } = new List<Category>();
        public virtual ICollection<Model> Models { get; set; } = new List<Model>();

        public Category()
        {
            Id = Guid.NewGuid();
        }
    }
}
