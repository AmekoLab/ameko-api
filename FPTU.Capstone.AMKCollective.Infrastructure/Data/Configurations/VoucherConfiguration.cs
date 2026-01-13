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

            // Relationships
            builder.HasOne(v => v.Creator)
                .WithMany(u => u.CreatedVouchers)
                .HasForeignKey(v => v.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
