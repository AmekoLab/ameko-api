using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
    {
        public void Configure(EntityTypeBuilder<Voucher> builder)
        {
            builder.ToTable("Vouchers");
            builder.HasKey(v => v.Id);

            builder.Property(v => v.Code)
                .IsRequired()
                .HasMaxLength(50);

            builder.HasIndex(v => v.Code).IsUnique();

            builder.Property(v => v.Name).HasMaxLength(255).IsRequired();
            builder.Property(v => v.Description).HasMaxLength(1000);

            builder.Property(v => v.Value).HasPrecision(18, 2);
            builder.Property(v => v.MaxDiscountAmount).HasPrecision(18, 2);
            builder.Property(v => v.MinOrderValue).HasPrecision(18, 2);

            // Enum Conversion
            builder.Property(v => v.Type).HasConversion<string>();
            builder.Property(v => v.DiscountType).HasConversion<string>();
            builder.Property(v => v.Status).HasConversion<string>();

            // Relationships
            builder.HasOne(v => v.Creator)
                .WithMany(u => u.CreatedVouchers)
                .HasForeignKey(v => v.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(v => v.TargetUser)
                .WithMany()
                .HasForeignKey(v => v.TargetUserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
