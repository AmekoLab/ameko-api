using FPTU.Capstone.AMKCollective.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class ReputationLogConfiguration : IEntityTypeConfiguration<ReputationLog>
    {
        public void Configure(EntityTypeBuilder<ReputationLog> builder)
        {
            builder.ToTable("ReputationLogs");

            builder.Property(x => x.TargetType)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(x => x.Reason)
                .HasMaxLength(512);

            builder.Property(x => x.ReferenceType)
                .HasMaxLength(100);

            builder.Property(x => x.ReferenceId)
                .HasMaxLength(100);

            builder.HasIndex(x => new { x.TargetType, x.TargetId, x.CreatedAt });
        }
    }
}
