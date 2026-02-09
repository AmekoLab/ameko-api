using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class CategoryConfiguration : IEntityTypeConfiguration<Category>
    {
        public void Configure(EntityTypeBuilder<Category> builder)
        {
            builder.ToTable("Categories");

            // PK
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Id)
                .ValueGeneratedNever(); 
            builder.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(c => c.ThumbnailURL)
                .HasMaxLength(500);

            builder.Property(c => c.Slug)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(c => c.IsActive)
                .HasDefaultValue(true);

            builder.Property(c => c.IsDeleted)
                .HasDefaultValue(false);
            builder.HasOne(c => c.Parent)
                .WithMany(c => c.SubCategories)
                .HasForeignKey(c => c.ParentId)
                .OnDelete(DeleteBehavior.Restrict); 

            // --- INDEXES ---
            // Foreign key to Shop (if category is shop-specific)
            builder.HasOne(c => c.Shop)
                .WithMany() // ShopProfile does not have Categories collection yet, or it's uni-directional
                .HasForeignKey(c => c.ShopId)
                .OnDelete(DeleteBehavior.Cascade); // If shop is deleted, delete its private categories

            builder.HasIndex(c => c.ShopId);
            
            // Indexes for querying
            builder.HasIndex(c => c.ParentId);
            builder.HasIndex(c => c.IsActive);
            builder.HasIndex(c => c.IsDeleted);
            builder.HasIndex(c => c.Name);

            // Composite index for fast filtering by shop and active status
            builder.HasIndex(c => new { c.ShopId, c.IsActive });

            // Unique constraint on Slug per Shop (or globally if ShopId is null)
            // Note: Slug must be unique within same shop, but multiple shops can have same slug
            builder.HasIndex(c => new { c.Slug, c.ShopId })
                .IsUnique();
        }
    }
}
