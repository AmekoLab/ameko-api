using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class ShopProfile : BaseEntity
    {
        public Guid UserId { get; set; }
        public string? Bio { get; set; }
        public string? Location { get; set; }

        // Navigation Properties
        public virtual User User { get; set; } = null!;
        public virtual ICollection<Model> Models { get; set; } = new List<Model>();
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
