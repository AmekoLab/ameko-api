using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class OrderGroupConfiguration : IEntityTypeConfiguration<OrderGroup>
    {
        public void Configure(EntityTypeBuilder<OrderGroup> builder)
        {
            builder.ToTable("OrderGroups");
            builder.HasKey(og => og.Id);

            builder.Property(og => og.TotalGroupAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(og => og.PaymentStatus)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");

            
        }
    }
}
