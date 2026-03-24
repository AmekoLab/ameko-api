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

            builder.Property(c => c.Id)
                .ValueGeneratedOnAdd();

            builder.Property(c => c.Name)
                .HasMaxLength(255);

            builder.Property(c => c.Image)
                .HasMaxLength(1000);

            builder.HasMany(c => c.UserConversations)
                .WithOne(uc => uc.Conversation)
                .HasForeignKey(uc => uc.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
