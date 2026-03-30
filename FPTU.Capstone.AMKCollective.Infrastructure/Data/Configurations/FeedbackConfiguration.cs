using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class FeedbackConfiguration : IEntityTypeConfiguration<Feedback>
    {
        public void Configure(EntityTypeBuilder<Feedback> builder)
        {
            builder.ToTable("Feedbacks");
            builder.HasKey(f => f.Id);

            builder.Property(f => f.Location)
                .HasMaxLength(500);

            // Relationships
            builder.HasOne(f => f.Order)
                .WithMany(o => o.Feedbacks)
                .HasForeignKey(f => f.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(f => f.FromUser)
                .WithMany(u => u.SentFeedbacks)
                .HasForeignKey(f => f.FromUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(f => f.Shop)
                .WithMany(s => s.Feedbacks)
                .HasForeignKey(f => f.ShopId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(f => f.Images)
                .WithOne(fi => fi.Feedback)
                .HasForeignKey(fi => fi.FeedbackId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
