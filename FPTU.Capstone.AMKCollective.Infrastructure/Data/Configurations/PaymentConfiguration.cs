using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> builder)
        {
            builder.ToTable("Payments");
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Amount)
                .HasPrecision(18, 2)
                .IsRequired();
            builder.Property(p => p.FeeAmount)
                .HasPrecision(18, 2) 
                .HasDefaultValue(0);
            builder.Property(p => p.StripeSessionId)
                .HasMaxLength(255);
            builder.Property(p => p.StripePaymentIntentId)
                .HasMaxLength(255);
            builder.Property(p => p.Status);
            builder.Property(p => p.Type)
                .IsRequired();
            builder.HasOne(p => p.User)               
                .WithMany(u => u.Payments)              
                .HasForeignKey(p => p.UserId)         
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(p => p.OrderGroup)
                .WithMany(og => og.Payments)
                .HasForeignKey(p => p.OrderGroupId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasIndex(p => p.StripeSessionId);
            builder.HasIndex(p => p.StripePaymentIntentId);

        }
    }
}
