using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
    {
        public void Configure(EntityTypeBuilder<OrderItem> builder)
        {
            builder.ToTable("OrderItems");
            builder.HasKey(oi => oi.Id);

            builder.Property(oi => oi.DesignConfig)
                .HasColumnType("text");

            builder.Property(oi => oi.UnitPrice)
                .HasPrecision(18, 2);

            builder.Property(oi => oi.TotalPrice)
                .HasPrecision(18, 2);

            builder.Property(oi => oi.DiscountAmount)
                .HasPrecision(18, 2);

            builder.Property(oi => oi.ItemStatus)
                .HasMaxLength(50);

            builder.Property(oi => oi.Notes)
                .HasMaxLength(1000);

            builder.Property(oi => oi.ItemType)
                .HasMaxLength(50);

            // Relationships
            builder.HasOne(oi => oi.AssembledProduct)
                .WithMany(ap => ap.OrderItems)
                .HasForeignKey(oi => oi.AssembledProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
