using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> builder)
        {
            builder.ToTable("Notifications");
            builder.HasKey(n => n.Id);

            // Auto-increment for int PK
            builder.Property(n => n.Id)
                .ValueGeneratedOnAdd();

            builder.Property(n => n.Title)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(n => n.Message)
                .HasMaxLength(1000);

            builder.Property(n => n.Type)
                .HasConversion<string>()
                .HasMaxLength(50);

            // Performance Indexes
            builder.HasIndex(n => n.UserId);
            builder.HasIndex(n => new { n.UserId, n.CreatedAt }).IsDescending(false, true);
            builder.HasIndex(n => new { n.UserId, n.IsRead });

            // Relationships
            builder.HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
