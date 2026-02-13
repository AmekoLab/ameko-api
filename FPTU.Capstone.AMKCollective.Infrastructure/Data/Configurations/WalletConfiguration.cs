using FPTU.Capstone.AMKCollective.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
    {
        public void Configure(EntityTypeBuilder<Wallet> builder)
        {
            builder.ToTable("Wallets");
            builder.HasKey(w => w.Id);

            builder.Property(w => w.Balance)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(w => w.HeldBalance)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(w => w.Currency)
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(x => x.PinHash)
                .HasMaxLength(255)
                .IsRequired(false); 

           
            builder.Property(x => x.PinResetCode)
                .HasMaxLength(6)
                .IsFixedLength() // it is always 6 characters 
                .IsRequired(false);

            builder.Property(x => x.PinResetExpiry)
                .IsRequired(false);

            // One-to-One relationship with User
            builder.HasOne(w => w.User)
                .WithOne(u => u.Wallet)
                .HasForeignKey<Wallet>(w => w.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // One-to-Many relationship with Payment
            builder.HasMany(w => w.Payments)
                .WithOne(p => p.Wallet)
                .HasForeignKey(p => p.WalletId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(w => w.UserId)
                .IsUnique();


        }
    }
}
