using FPTU.Capstone.AMKCollective.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class OrderIssueLogConfiguration : IEntityTypeConfiguration<OrderIssueLog>
    {
        public void Configure(EntityTypeBuilder<OrderIssueLog> builder)
        {
            builder.ToTable("OrderIssueLogs");
            builder.HasKey(oil => oil.Id);

            builder.Property(oil => oil.ActionByRole)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(oil => oil.Action)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(oil => oil.Comment)
                .HasMaxLength(2000);

            builder.Property(oil => oil.EvidenceUrl)
                .HasMaxLength(2000);

            // Relationships
            builder.HasOne(oil => oil.OrderIssue)
                .WithMany(oi => oi.Logs)
                .HasForeignKey(oil => oil.OrderIssueId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(oil => oil.ActionBy)
                .WithMany(u => u.OrderIssueActions)
                .HasForeignKey(oil => oil.ActionById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(oil => oil.OrderIssueId);
            builder.HasIndex(oil => oil.ActionById);
            builder.HasIndex(oil => oil.CreatedAt);
        }
    }
}
