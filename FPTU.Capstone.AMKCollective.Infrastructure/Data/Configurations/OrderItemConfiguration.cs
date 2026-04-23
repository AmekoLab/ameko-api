using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
    {
        public void Configure(EntityTypeBuilder<OrderItem> builder)
        {
            builder.ToTable("OrderItems");
            builder.HasKey(oi => oi.Id);

            builder.Property(oi => oi.ProductName).HasMaxLength(255).IsRequired();
            builder.Property(oi => oi.ProductImage).HasMaxLength(500);

            builder.Property(oi => oi.UnitPrice).HasPrecision(18, 2);
            builder.Property(oi => oi.TotalPrice).HasPrecision(18, 2);
            builder.Property(oi => oi.DiscountAmount).HasPrecision(18, 2);
            builder.Property(oi => oi.SystemAllocatedDiscount).HasPrecision(18, 2).HasDefaultValue(0).ValueGeneratedNever();
            builder.Property(oi => oi.ShopAllocatedDiscount).HasPrecision(18, 2).HasDefaultValue(0).ValueGeneratedNever();
            builder.Property(oi => oi.AllocatedDiscount).HasPrecision(18, 2).HasDefaultValue(0).ValueGeneratedNever();
            builder.Property(oi => oi.FinalPrice).HasPrecision(18, 2).HasDefaultValue(0).ValueGeneratedNever();
            builder.Property(oi => oi.ItemStatus).HasDefaultValue(OrderItemStatus.Active).ValueGeneratedNever();

            builder.Property(oi => oi.DesignConfig).HasColumnType("json"); 

            builder.HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade); 

            builder.HasOne(oi => oi.AssembledProduct)
                .WithMany(ap => ap.OrderItems)
                .HasForeignKey(oi => oi.AssembledProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            builder.HasOne(oi => oi.Product)
                .WithMany()
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        }
    }
}
