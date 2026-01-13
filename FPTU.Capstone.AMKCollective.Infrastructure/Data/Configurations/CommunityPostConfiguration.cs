using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class CommunityPostConfiguration : IEntityTypeConfiguration<CommunityPost>
    {
        public void Configure(EntityTypeBuilder<CommunityPost> builder)
        {
            builder.ToTable("CommunityPosts");
            builder.HasKey(cp => cp.Id);

            builder.Property(cp => cp.Title)
                .IsRequired()
                .HasMaxLength(500);

            // Relationships
            builder.HasOne(cp => cp.User)
                .WithMany(u => u.CommunityPosts)
                .HasForeignKey(cp => cp.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
