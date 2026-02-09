using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class KitDesignOptionConfiguration : IEntityTypeConfiguration<KitDesignOption>
    {
        public void Configure(EntityTypeBuilder<KitDesignOption> builder)
        {
            builder.ToTable("KitDesignOptions");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.LayerImageUrl)
                .HasMaxLength(500);

            builder.Property(x => x.StepName)
                .HasMaxLength(255);

            builder.Property(x => x.IsDefault)
                .HasDefaultValue(false);

            builder.Property(x => x.Tags)
                .HasMaxLength(500) 
                .IsRequired(false);

            builder.Property(x => x.NextStepFilterRule)
                .HasMaxLength(500) 
                .IsRequired(false);

            // ----------------------------
            // BaseKit relationship
            // ----------------------------
            builder.HasOne(x => x.BaseKit)
                .WithMany(m => m.AsBaseKitOptions)
                .HasForeignKey(x => x.BaseKitId)
                .OnDelete(DeleteBehavior.Restrict);

            // ----------------------------
            // Component relationship
            // ----------------------------
            builder.HasOne(x => x.Component)
                .WithMany(m => m.AsComponentOptions)
                .HasForeignKey(x => x.ComponentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ----------------------------
            // Indexes (recommend)
            // ----------------------------
            builder.HasIndex(x => new { x.BaseKitId, x.StepOrder });

            // Cho phép cùng 1 Component có nhiều bản ghi với Tags khác nhau (Nhánh hình ảnh tích lũy)
            // KHÔNG dùng unique trên (BaseKitId, ComponentId) nữa
            builder.HasIndex(x => new { x.BaseKitId, x.ComponentId });

            // Index mới phục vụ lọc theo nhánh
            builder.HasIndex(x => new { x.BaseKitId, x.StepName, x.Tags });
        }
    }
}
