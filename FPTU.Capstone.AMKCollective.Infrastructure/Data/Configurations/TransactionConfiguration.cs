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
            builder.ToTable("Transactions", t =>
            {
                t.HasCheckConstraint("CK_Transaction_FeeAmount_Positive", "`FeeAmount` >= 0");
                t.HasCheckConstraint(
                    "CK_Transaction_MutualExclusive_OrderRef",
                    "`OrderGroupId` IS NULL OR `RelatedOrderId` IS NULL"
                );
            });

            builder.HasKey(t => t.Id);

            builder.Property(t => t.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(t => t.BalanceAfterTransaction)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(t => t.HeldBalanceAfterTransaction)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(t => t.FeeAmount)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m)
                .IsRequired();

            builder.Property(t => t.Direction)
                .IsRequired();

            builder.Property(t => t.Type)
                .IsRequired();

            builder.Property(t => t.Currency)
                .IsRequired()
                .HasMaxLength(3)
                .HasDefaultValue("VND");

            builder.Property(t => t.Description)
                .HasMaxLength(500);

            builder.Property(t => t.WalletId)
                .IsRequired();

            // Indexes
            builder.HasIndex(t => t.WalletId)
                .HasDatabaseName("IX_Transactions_WalletId");

            builder.HasIndex(t => t.OrderGroupId)
                .HasDatabaseName("IX_Transactions_OrderGroupId");

            builder.HasIndex(t => t.RelatedOrderId)
                .HasDatabaseName("IX_Transactions_RelatedOrderId");

            // Relationships
            builder.HasOne(t => t.Wallet)
                .WithMany(w => w.Transactions)
                .HasForeignKey(t => t.WalletId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(t => t.OrderGroup)
                .WithMany(og => og.Transactions)
                .HasForeignKey(t => t.OrderGroupId)
                .OnDelete(DeleteBehavior.Restrict); // Đổi từ SetNull sang Restrict cho an toàn dữ liệu tài chính

            builder.HasOne(t => t.RelatedOrder)
                .WithMany(o => o.Transactions)
                .HasForeignKey(t => t.RelatedOrderId)
                .OnDelete(DeleteBehavior.Restrict); // Đổi từ SetNull sang Restrict
        }
    }
}