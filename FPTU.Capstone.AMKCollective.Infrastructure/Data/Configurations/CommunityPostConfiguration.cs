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

            // Auto-increment for int PK
            builder.Property(cp => cp.Id)
                .ValueGeneratedOnAdd();

            builder.Property(cp => cp.Title)
                .IsRequired()
                .HasMaxLength(1000);

            // Relationships
            builder.HasOne(cp => cp.User)
                .WithMany(u => u.CommunityPosts)
                .HasForeignKey(cp => cp.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
