using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class PostCommentConfiguration : IEntityTypeConfiguration<PostComment>
    {
        public void Configure(EntityTypeBuilder<PostComment> builder)
        {
            builder.ToTable("PostComments");
            builder.HasKey(pc => pc.Id);

            // Auto-increment for int PK
            builder.Property(pc => pc.Id)
                .ValueGeneratedOnAdd();

            builder.Property(pc => pc.Content)
                .IsRequired()
                .HasColumnType("text");

            // Relationships
            builder.HasOne(pc => pc.Post)
                .WithMany(cp => cp.PostComments)
                .HasForeignKey(pc => pc.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(pc => pc.User)
                .WithMany(u => u.PostComments)
                .HasForeignKey(pc => pc.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
