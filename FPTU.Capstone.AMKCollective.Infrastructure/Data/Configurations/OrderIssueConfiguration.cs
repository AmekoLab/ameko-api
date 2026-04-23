using FPTU.Capstone.AMKCollective.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class OrderIssueConfiguration : IEntityTypeConfiguration<OrderIssue>
    {
        public void Configure(EntityTypeBuilder<OrderIssue> builder)
        {
            builder.ToTable("OrderIssues");
            builder.HasKey(oi => oi.Id);

            builder.Property(oi => oi.Type)
                .IsRequired();

            builder.Property(oi => oi.Status)
                .IsRequired();

            builder.Property(oi => oi.Reason)
                .HasMaxLength(500);

            builder.Property(oi => oi.Description)
                .HasMaxLength(2000);

            builder.Property(oi => oi.EvidenceUrl)
                .HasMaxLength(2000);

            builder.Property(oi => oi.ShopResponse)
                .HasMaxLength(2000);

            builder.Property(oi => oi.AdminNote)
                .HasMaxLength(2000);

            builder.Property(oi => oi.AIAnalysisResult)
                .HasColumnType("longtext")
                .IsRequired(false);

            builder.Property(oi => oi.CancelledItemIds)
                .HasColumnType("json")
                .IsRequired(false);

            // Relationships
            builder.HasOne(oi => oi.Order)
                .WithMany(o => o.OrderIssues)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(oi => oi.User)
                .WithMany(u => u.OrderIssues)
                .HasForeignKey(oi => oi.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(oi => oi.OrderId);
            builder.HasIndex(oi => oi.UserId);
            builder.HasIndex(oi => oi.Status);
            builder.HasIndex(oi => new { oi.CreatedAt, oi.IsDeleted })
                   .HasDatabaseName("IX_OrderIssues_Dashboard");
        }
    }
}
