using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class ProductAssembledDetailConfiguration : IEntityTypeConfiguration<ProductAssembledDetail>
    {
        public void Configure(EntityTypeBuilder<ProductAssembledDetail> builder)
        {
            builder.ToTable("ProductAssembledDetails");
            builder.HasKey(p => p.Id);

            // Relationships
            builder.HasOne(p => p.AssembledProduct)
                .WithMany(a => a.ProductAssembledDetails)
                .HasForeignKey(p => p.AssembledProductId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(p => p.BaseKit)
                .WithMany(m => m.ProductAssembledDetails)
                .HasForeignKey(p => p.BaseKitId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(p => p.Component)
                .WithMany()
                .HasForeignKey(p => p.ComponentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(p => p.SoundUrl)
                .HasMaxLength(500);
        }
    }
}
