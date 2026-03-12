using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.ToTable("Orders");
            builder.HasKey(o => o.Id);

            builder.Property(x => x.ReceiverName)
                .HasMaxLength(100)
                .IsRequired(false); 

            builder.Property(x => x.ReceiverPhone)
                .HasMaxLength(20)
                .IsRequired(false); 

            builder.Property(x => x.ShippingAddress)
                .HasMaxLength(500)
                .IsRequired(false); 

            builder.Property(x => x.Note)
                .HasMaxLength(500)
                .IsRequired(false); 

            builder.Property(o => o.CancelReason)
                .HasMaxLength(500);
            builder.Property(x => x.PaymentStatus)
                .HasDefaultValue(PaymentStatus.Pending);
            builder.Property(o => o.OrderStatus)
                .HasDefaultValue(OrderStatus.Pending);
            builder.Property(o => o.SubTotal).HasPrecision(18, 2);
            builder.Property(o => o.ShippingFee).HasPrecision(18, 2).HasDefaultValue(0);
            builder.Property(o => o.TotalAmount).HasPrecision(18, 2);

           

            //Relationships
            builder.HasOne(o => o.OrderGroup)
                .WithMany(og => og.Orders)
                .HasForeignKey(o => o.OrderGroupId)
                .OnDelete(DeleteBehavior.SetNull); 


            builder.HasOne(o => o.Customer)
                .WithMany(u => u.CustomerOrders)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict); 

            builder.HasOne(o => o.Shop)
                .WithMany(s => s.Orders)
                .HasForeignKey(o => o.ShopId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        }
    }
}
