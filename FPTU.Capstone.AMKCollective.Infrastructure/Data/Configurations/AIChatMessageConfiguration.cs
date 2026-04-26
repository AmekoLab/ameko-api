using FPTU.Capstone.AMKCollective.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class AIChatMessageConfiguration : IEntityTypeConfiguration<AIChatMessage>
    {
        public void Configure(EntityTypeBuilder<AIChatMessage> builder)
        {
            builder.ToTable("AIChatMessages");

            builder.HasKey(m => m.Id);
            builder.Property(m => m.Id).ValueGeneratedOnAdd();

            builder.Property(m => m.Role)
                .IsRequired()
                .HasMaxLength(20); // "user" | "assistant"

            builder.Property(m => m.Content)
                .IsRequired()
                .HasColumnType("text");

            // RecommendationPayload: JSON serialized items list, dùng longtext vì có thể rất dài
            builder.Property(m => m.RecommendationPayload)
                .HasColumnType("longtext");

            // Index để query messages của 1 conversation nhanh, theo đúng thứ tự
            builder.HasIndex(m => m.ConversationId);
        }
    }
}
