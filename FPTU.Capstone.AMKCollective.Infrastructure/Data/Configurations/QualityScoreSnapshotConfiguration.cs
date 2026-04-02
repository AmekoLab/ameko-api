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
    public class QualityScoreSnapshotConfiguration : IEntityTypeConfiguration<QualityScoreSnapshot>
    {
        public void Configure(EntityTypeBuilder<QualityScoreSnapshot> builder)
        {
            builder.ToTable("QualityScoreSnapshots");
            builder.HasKey(q => q.Id);

            builder.Property(q => q.Badge)
                .HasConversion<int>();

            // Relationship: 1-N ShopProfile
            builder.HasOne(q => q.Shop)
                .WithMany(s => s.QualityScoreSnapshots)
                .HasForeignKey(q => q.ShopId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}