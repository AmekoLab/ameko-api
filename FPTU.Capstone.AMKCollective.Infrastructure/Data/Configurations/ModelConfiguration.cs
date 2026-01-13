using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class ModelConfiguration : IEntityTypeConfiguration<Model>
    {
        public void Configure(EntityTypeBuilder<Model> builder)
        {
            builder.ToTable("Models");
            builder.HasKey(m => m.Id);

            builder.Property(m => m.Name)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(m => m.ThumbnailURL)
                .HasMaxLength(500);

            builder.Property(m => m.PartType)
                .HasMaxLength(100);

            builder.Property(m => m.Description)
                .HasMaxLength(2000);

            // Relationships
            builder.HasOne(m => m.Shop)
                .WithMany(s => s.Models)
                .HasForeignKey(m => m.ShopId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(m => m.Category)
                .WithMany(c => c.Models)
                .HasForeignKey(m => m.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
