using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
    {
        public void Configure(EntityTypeBuilder<Conversation> builder)
        {
            builder.ToTable("Conversations");
            builder.HasKey(c => c.Id);

            // Relationships
            builder.HasOne(c => c.UserOne)
                .WithMany(u => u.ConversationsAsUserOne)
                .HasForeignKey(c => c.UserOneId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(c => c.UserTwo)
                .WithMany(u => u.ConversationsAsUserTwo)
                .HasForeignKey(c => c.UserTwoId)
                .OnDelete(DeleteBehavior.Restrict);

            // Unique constraint: Only one conversation between two users
            builder.HasIndex(c => new { c.UserOneId, c.UserTwoId }).IsUnique();
        }
    }
}
