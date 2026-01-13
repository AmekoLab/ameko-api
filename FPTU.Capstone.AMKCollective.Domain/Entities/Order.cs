using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class Order
    {
        public Guid Id { get; set; }
        public Guid? OrderGroupId { get; set; }
        public Guid? VoucherId { get; set; }
        public Guid CustomerId { get; set; }
        public Guid ShopId { get; set; }
        public decimal TotalAmount { get; set; }

        // Navigation Properties
        public virtual OrderGroup? OrderGroup { get; set; }
        public virtual Voucher? Voucher { get; set; }
        public virtual User Customer { get; set; } = null!;
        public virtual ShopProfile Shop { get; set; } = null!;
        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

        public Order()
        {
            Id = Guid.NewGuid();
        }
    }
}
