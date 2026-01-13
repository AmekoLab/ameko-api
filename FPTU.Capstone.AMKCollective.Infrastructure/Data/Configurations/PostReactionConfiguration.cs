using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class PostReactionConfiguration : IEntityTypeConfiguration<PostReaction>
    {
        public void Configure(EntityTypeBuilder<PostReaction> builder)
        {
            builder.ToTable("PostReactions");
            builder.HasKey(pr => pr.Id);

            builder.Property(pr => pr.Type)
                .IsRequired()
                .HasMaxLength(50);

            // Relationships
            builder.HasOne(pr => pr.Post)
                .WithMany(cp => cp.PostReactions)
                .HasForeignKey(pr => pr.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(pr => pr.User)
                .WithMany(u => u.PostReactions)
                .HasForeignKey(pr => pr.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Unique constraint: One reaction per user per post
            builder.HasIndex(pr => new { pr.PostId, pr.UserId }).IsUnique();
        }
    }
}
