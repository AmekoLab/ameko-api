using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
    {
        public void Configure(EntityTypeBuilder<Transaction> builder)
        {
            builder.ToTable("Transactions");
            builder.HasKey(t => t.Id);

            builder.Property(t => t.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(t => t.Currency)
                .IsRequired()
                .HasMaxLength(3)
                .HasDefaultValue("VND");

            builder.Property(t => t.Type)
                .IsRequired();

            builder.Property(t => t.Description)
                .HasMaxLength(500);

            builder.Property(t => t.WalletId).IsRequired();

            // Relationships
            builder.HasOne(t => t.Wallet)
                .WithMany(w => w.Transactions)
                .HasForeignKey(t => t.WalletId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(t => t.OrderGroup)
                .WithMany(og => og.Transactions)
                .HasForeignKey(t => t.OrderGroupId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(t => t.RelatedOrder)
                .WithMany(o => o.Transactions)
                .HasForeignKey(t => t.RelatedOrderId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}