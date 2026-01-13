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
            builder.HasKey(k => k.Id);

            builder.Property(k => k.LayerImageUrl)
                .HasMaxLength(500);

            builder.Property(k => k.StepName)
                .HasMaxLength(255);

            // Relationships
            builder.HasOne(k => k.BaseKit)
                .WithMany(m => m.KitDesignOptions)
                .HasForeignKey(k => k.BaseKitId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(k => k.Component)
                .WithMany()
                .HasForeignKey(k => k.ComponentId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
