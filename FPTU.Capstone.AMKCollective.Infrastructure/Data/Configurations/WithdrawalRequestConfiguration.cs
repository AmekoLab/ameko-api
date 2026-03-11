using FPTU.Capstone.AMKCollective.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class WithdrawalRequestConfiguration : IEntityTypeConfiguration<WithdrawalRequest>
    {
        public void Configure(EntityTypeBuilder<WithdrawalRequest> builder)
        {
            builder.ToTable("WithdrawalRequests");
            
            builder.HasKey(w => w.Id);

            builder.Property(w => w.Amount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(w => w.BankName).IsRequired().HasMaxLength(255);
            builder.Property(w => w.BankAccountNumber).IsRequired().HasMaxLength(255);
            builder.Property(w => w.BankAccountName).IsRequired().HasMaxLength(255);
            
            // Enum mapping
            builder.Property(w => w.Status).IsRequired();

            // Relationship 1: Customer -> WithdrawalRequest
            builder.HasOne(w => w.User)
                .WithMany(u => u.WithdrawalRequests)
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship 2: Admin -> WithdrawalRequest
            builder.HasOne(w => w.Admin)
                .WithMany(u => u.ApprovedWithdrawalRequests)
                .HasForeignKey(w => w.AdminId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
