using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class ModelConfiguration : IEntityTypeConfiguration<Model>
    {
        public void Configure(EntityTypeBuilder<Model> builder)
        {
            // Table & Key
            builder.ToTable("Models");
            builder.HasKey(x => x.Id);

            // BaseEntity fields
            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.IsDeleted)
                .HasDefaultValue(false);

            // Core fields
            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(x => x.Slug)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(x => x.PartType)
                .HasMaxLength(100);

            builder.Property(x => x.ThumbnailURL)
                .HasMaxLength(500);

            builder.Property(x => x.DefaultLayerImageUrl)
                .HasMaxLength(500);

            builder.Property(x => x.Description)
                .HasColumnType("text");

            // PostgreSQL JSON
            builder.Property(x => x.Specifications)
                .HasColumnType("json");

            builder.Property(x => x.Embedding)
                .HasColumnType("json");

            builder.Property(x => x.Price)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            builder.Property(x => x.StockQuantity)
                .IsRequired();

            builder.Property(x => x.IsActive)
                .HasDefaultValue(true);

            builder.Property(x => x.IsAddonEligible)
                .HasDefaultValue(false);

            // Indexes 

            builder.HasIndex(x => x.Slug)
                .IsUnique();

            // Builder / Search filter
            builder.HasIndex(x => new { x.PartType, x.IsActive });

            // Addon-options query: shop lọc part nào được phép custom per-key
            builder.HasIndex(x => new { x.ShopId, x.IsAddonEligible, x.IsActive });

            builder.HasIndex(x => x.ShopId);

            // Relationships
            builder.HasOne(x => x.Shop)
                .WithMany(s => s.Models)
                .HasForeignKey(x => x.ShopId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Category)
                .WithMany(c => c.Models)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // BaseKit to Options
            builder.HasMany(x => x.AsBaseKitOptions)
                .WithOne(o => o.BaseKit)
                .HasForeignKey(o => o.BaseKitId)
                .OnDelete(DeleteBehavior.Restrict);

            // Component to Options
            builder.HasMany(x => x.AsComponentOptions)
                .WithOne(o => o.Component)
                .HasForeignKey(o => o.ComponentId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
