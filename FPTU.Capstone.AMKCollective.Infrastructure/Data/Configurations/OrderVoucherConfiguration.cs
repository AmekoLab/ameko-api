using FPTU.Capstone.AMKCollective.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class OrderVoucherConfiguration : IEntityTypeConfiguration<OrderVoucher>
    {
        public void Configure(EntityTypeBuilder<OrderVoucher> builder)
        {
            builder.ToTable("OrderVouchers");
            builder.HasKey(ov => ov.Id);

            builder.Property(ov => ov.VoucherCode)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(ov => ov.DiscountApplied)
                .HasPrecision(18, 2);

            builder.Property(ov => ov.VoucherType);
            builder.Property(ov => ov.ApplyOrder);

            // Unique: 
            builder.HasIndex(ov => new { ov.OrderId, ov.VoucherId }).IsUnique();

            // Relationships
            builder.HasOne(ov => ov.Order)
                .WithMany(o => o.OrderVouchers)
                .HasForeignKey(ov => ov.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(ov => ov.Voucher)
                .WithMany(v => v.OrderVouchers)
                .HasForeignKey(ov => ov.VoucherId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
