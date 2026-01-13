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

            builder.Property(og => og.Amount)
                .HasPrecision(18, 2);

            builder.Property(og => og.BalanceBefore)
                .HasPrecision(18, 2);
        }
    }
}
