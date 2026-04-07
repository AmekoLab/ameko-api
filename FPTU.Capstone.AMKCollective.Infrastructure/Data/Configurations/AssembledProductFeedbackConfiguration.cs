using FPTU.Capstone.AMKCollective.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class AssembledProductFeedbackConfiguration : IEntityTypeConfiguration<AssembledProductFeedback>
    {
        public void Configure(EntityTypeBuilder<AssembledProductFeedback> builder)
        {
            builder.ToTable("AssembledProductFeedbacks");
            builder.HasKey(f => f.Id);

            builder.ToTable(t =>
                t.HasCheckConstraint("CK_AssembledProductFeedbacks_Rating_Range", "`Rating` >= 1 AND `Rating` <= 5"));

            builder.HasIndex(f => f.OrderItemId).IsUnique();

            builder.HasOne(f => f.OrderItem)
                .WithMany()
                .HasForeignKey(f => f.OrderItemId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(f => f.AssembledProduct)
                .WithMany(ap => ap.Feedbacks)
                .HasForeignKey(f => f.AssembledProductId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(f => f.FromUser)
                .WithMany()
                .HasForeignKey(f => f.FromUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(f => f.Shop)
                .WithMany()
                .HasForeignKey(f => f.ShopId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(f => f.Images)
                .WithOne(fi => fi.AssembledProductFeedback)
                .HasForeignKey(fi => fi.AssembledProductFeedbackId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
