using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class AssembledProductConfiguration : IEntityTypeConfiguration<AssembledProduct>
    {
        public void Configure(EntityTypeBuilder<AssembledProduct> builder)
        {
            builder.ToTable("AssembledProducts");
            builder.HasKey(a => a.Id);

            builder.Property(a => a.Name)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(a => a.View3DUrl)
                .HasMaxLength(500);

            builder.Property(a => a.Price)
                .HasPrecision(18, 2);
        }
    }
}
