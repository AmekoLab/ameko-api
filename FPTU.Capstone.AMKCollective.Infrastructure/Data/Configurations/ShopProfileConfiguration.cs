using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class ShopProfileConfiguration : IEntityTypeConfiguration<ShopProfile>
    {
        public void Configure(EntityTypeBuilder<ShopProfile> builder)
        {
            builder.ToTable("ShopProfiles");
            builder.HasKey(s => s.Id);

            builder.Property(s => s.ShopName)
                .IsRequired()
                .HasMaxLength(255);
            builder.Property(s => s.Bio)
                .HasMaxLength(1000);

            builder.Property(s => s.Address)
                .HasMaxLength(500);
            builder.Property(s => s.PhoneNumber)
                .HasMaxLength(20);
            builder.Property(s => s.ContactEmail)
                .HasMaxLength(255);
            //image
            builder.Property(s => s.LogoUrl)
                .HasMaxLength(500);
            builder.Property(s => s.BannerUrl)
                .HasMaxLength(500);

            //
            builder.Property(s => s.CitizenId)
                .HasMaxLength(50);
            builder.Property(s => s.TaxCode)
                .HasMaxLength(50);

            builder.Property(s => s.Status)
                .HasDefaultValue(ShopStatus.PendingApproval)
                .HasConversion<int>();
            builder.Property(s => s.IsActive)
                .HasDefaultValue(true);
            builder.Property(s => s.TotalSales)
                .HasDefaultValue(0);
            builder.Property(s => s.TotalRevenue)
                .HasPrecision(18, 2)
                .HasDefaultValue(0.00m);

            builder.Property(s => s.BankName)
                .HasMaxLength(100);
            builder.Property(s => s.BankAccountNumber).HasMaxLength(50);
            builder.Property(s => s.BankAccountName).HasMaxLength(100);

            builder.Property(s => s.AdminNote)
                .HasMaxLength(1000);

            builder.Property(s => s.Rating)
                .HasDefaultValue(0.0);

            builder.Property(s => s.TotalReviews)
                .HasDefaultValue(0);

            builder.Property(s => s.ResubmitCount)
                .HasDefaultValue(0);
            builder.Property(s => s.CurrentQualityScore)
                .HasDefaultValue(50);

            builder.Property(s => s.Badge)
                .HasDefaultValue(ShopBadge.Basic)
                .HasConversion<int>();

            builder.HasIndex(s => s.ShopName);
            builder.HasIndex(s => s.UserId).IsUnique();
            builder.HasIndex(s => s.CitizenId)
                .IsUnique()
                .HasFilter("`CitizenId` IS NOT NULL");

            builder.HasOne(s => s.User)
                .WithOne(u => u.ShopProfile)
                .HasForeignKey<ShopProfile>(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            

        }
    }
}
