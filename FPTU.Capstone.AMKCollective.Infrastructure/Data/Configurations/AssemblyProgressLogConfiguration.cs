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
    public class AssemblyProgressLogConfiguration : IEntityTypeConfiguration<AssemblyProgressLog>
    {
        public void Configure(EntityTypeBuilder<AssemblyProgressLog> builder)
        {
            builder.ToTable("AssemblyProgressLogs");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.StepName).IsRequired().HasMaxLength(200);
            builder.Property(x => x.Note).HasMaxLength(1000);
            builder.Property(x => x.MediaUrl).HasMaxLength(500);

            // Relates to OrderItem (1-N)
            builder.HasOne(x => x.OrderItem)
                   .WithMany(o => o.AssemblyProgressLogs)
                   .HasForeignKey(x => x.OrderItemId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
