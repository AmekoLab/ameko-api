using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class CommunityAttachmentConfiguration : IEntityTypeConfiguration<CommunityAttachment>
    {
        public void Configure(EntityTypeBuilder<CommunityAttachment> builder)
        {
            builder.ToTable("CommunityAttachments");
            builder.HasKey(ca => ca.Id);

            // Auto-increment for int PK
            builder.Property(ca => ca.Id)
                .ValueGeneratedOnAdd();

            builder.Property(ca => ca.FileUrl)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(ca => ca.FileType)
                .HasMaxLength(50);

            // Relationships
            builder.HasOne(ca => ca.Post)
                .WithMany(cp => cp.Attachments)
                .HasForeignKey(ca => ca.PostId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
