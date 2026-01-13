using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class VoucherUsageLogConfiguration : IEntityTypeConfiguration<VoucherUsageLog>
    {
        public void Configure(EntityTypeBuilder<VoucherUsageLog> builder)
        {
            builder.ToTable("VoucherUsageLogs");
            builder.HasKey(vul => vul.Id);

            builder.Property(vul => vul.Code)
                .IsRequired()
                .HasMaxLength(50);

            // Relationships
            builder.HasOne(vul => vul.User)
                .WithMany(u => u.VoucherUsageLogs)
                .HasForeignKey(vul => vul.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(vul => vul.Voucher)
                .WithMany(v => v.VoucherUsageLogs)
                .HasForeignKey(vul => vul.VoucherId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
