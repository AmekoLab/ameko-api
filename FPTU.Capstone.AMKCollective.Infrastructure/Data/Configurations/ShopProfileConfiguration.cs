using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class ShopProfileConfiguration : IEntityTypeConfiguration<ShopProfile>
    {
        public void Configure(EntityTypeBuilder<ShopProfile> builder)
        {
            builder.ToTable("ShopProfiles");
            builder.HasKey(s => s.Id);

            builder.Property(s => s.Bio)
                .HasMaxLength(1000);

            builder.Property(s => s.Location)
                .HasMaxLength(500);

            // One-to-one relationship with User
            builder.HasOne(s => s.User)
                .WithOne(u => u.ShopProfile)
                .HasForeignKey<ShopProfile>(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(s => s.UserId).IsUnique();
        }
    }
}
