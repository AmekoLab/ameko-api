using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class MessageConfiguration : IEntityTypeConfiguration<Message>
    {
        public void Configure(EntityTypeBuilder<Message> builder)
        {
            builder.ToTable("Messages");
            builder.HasKey(m => m.Id);

            // Auto-increment for int PK
            builder.Property(m => m.Id)
                .ValueGeneratedOnAdd();

            builder.Property(m => m.Content)
                .IsRequired()
                .HasColumnType("text");

            builder.Property(m => m.AttachmentUrl)
                .HasMaxLength(1000);

            // Relationships
            builder.HasOne(m => m.Sender)
                .WithMany(u => u.MessagesCreated)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(m => m.ParentMessage)
                .WithMany(m => m.Replies)
                .HasForeignKey(m => m.ParentMessageId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(m => m.MessageRecipients)
                .WithOne(mr => mr.Message)
                .HasForeignKey(mr => mr.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
