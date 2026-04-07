using FPTU.Capstone.AMKCollective.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class FeedbackImageConfiguration : IEntityTypeConfiguration<FeedbackImage>
    {
        public void Configure(EntityTypeBuilder<FeedbackImage> builder)
        {
            // Tên bảng trong Database
            builder.ToTable("FeedbackImages");
            builder.ToTable(t => t.HasCheckConstraint(
                "CK_FeedbackImages_ExclusiveArc",
                "(`FeedbackId` IS NOT NULL AND `AssembledProductFeedbackId` IS NULL) OR (`FeedbackId` IS NULL AND `AssembledProductFeedbackId` IS NOT NULL)"));
            builder.HasKey(fi => fi.Id);
            builder.Property(fi => fi.ImageUrl)
                .IsRequired()
                .HasMaxLength(500);

            // Relationship (1-N Feedback)
            builder.HasOne(fi => fi.Feedback)
                .WithMany(f => f.Images)
                .HasForeignKey(fi => fi.FeedbackId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(fi => fi.AssembledProductFeedback)
                .WithMany(f => f.Images)
                .HasForeignKey(fi => fi.AssembledProductFeedbackId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
