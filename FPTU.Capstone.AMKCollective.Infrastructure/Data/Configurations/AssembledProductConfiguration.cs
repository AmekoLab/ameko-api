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

            builder.Property(a => a.Image1)
                .HasMaxLength(500);

            builder.Property(a => a.Image2)
                .HasMaxLength(500);

            builder.Property(a => a.Image3)
                .HasMaxLength(500);

            builder.Property(a => a.Description)
                .HasMaxLength(2000);

            builder.Property(a => a.Quantity);

            builder.Property(a => a.Layout)
                .HasMaxLength(255);

            builder.Property(a => a.Mounting)
                .HasMaxLength(255);

            builder.Property(a => a.PCB)
                .HasMaxLength(255);

            builder.Property(a => a.Connection)
                .HasMaxLength(255);

            builder.Property(a => a.Battery)
                .HasMaxLength(255);

            builder.Property(a => a.Rating)
                .HasDefaultValue(0.0);

            builder.Property(a => a.TotalReviews)
                .HasDefaultValue(0);

            builder.Property(a => a.Embedding)
                .HasColumnType("json");
        }
    }
}
