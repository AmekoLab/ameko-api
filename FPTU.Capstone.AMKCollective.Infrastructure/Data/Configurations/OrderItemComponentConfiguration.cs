using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class OrderItemComponentConfiguration : IEntityTypeConfiguration<OrderItemComponent>
    {
        public void Configure(EntityTypeBuilder<OrderItemComponent> builder)
        {
            builder.ToTable("OrderItemComponents");
            builder.HasKey(oic => oic.Id);

            builder.Property(oic => oic.PartPriceSnapshot)
                .HasPrecision(18, 2);

            builder.Property(oic => oic.PartName)
                .HasMaxLength(255);

            builder.Property(oic => oic.PartImageUrl)
                .HasMaxLength(500);

            builder.Property(oic => oic.Notes)
                .HasMaxLength(1000);

            // Relationships
            builder.HasOne(oic => oic.OrderItem)
                .WithMany(oi => oi.OrderItemComponents)
                .HasForeignKey(oic => oic.OrderItemId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(oic => oic.Part)
                .WithMany()
                .HasForeignKey(oic => oic.PartId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
