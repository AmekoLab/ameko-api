using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> builder)
        {
            builder.ToTable("Payments");
            builder.HasKey(p => p.Id);

            builder.Property(p => p.AddressLine)
                .HasMaxLength(500);

            builder.Property(p => p.WardName)
                .HasMaxLength(255);

            // One-to-one relationship with OrderGroup
            builder.HasOne(p => p.OrderGroup)
                .WithOne(og => og.Payment)
                .HasForeignKey<Payment>(p => p.OrderGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(p => p.OrderGroupId).IsUnique();
        }
    }
}
