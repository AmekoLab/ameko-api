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
    public class AssemblyStepTemplateConfiguration : IEntityTypeConfiguration<AssemblyStepTemplate>
    {
        public void Configure(EntityTypeBuilder<AssemblyStepTemplate> builder)
        {
            builder.ToTable("AssemblyStepTemplates");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.StepName).IsRequired().HasMaxLength(200);

            // Relates to ShopProfile (1-N)
            builder.HasOne(x => x.Shop)
                   .WithMany(s => s.AssemblyStepTemplates)
                   .HasForeignKey(x => x.ShopId)
                   .OnDelete(DeleteBehavior.Cascade); 
        }
    }
}