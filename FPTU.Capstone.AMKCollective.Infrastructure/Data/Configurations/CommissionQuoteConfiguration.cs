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
    public class CommissionQuoteConfiguration : IEntityTypeConfiguration<CommissionQuote>
    {
        public void Configure(EntityTypeBuilder<CommissionQuote> builder)
        {
            builder.ToTable("CommissionQuotes");

            builder.HasOne(cq => cq.CommissionRequest)
                .WithMany(cr => cr.Quotes)
                .HasForeignKey(cq => cq.CommissionRequestId)
                .OnDelete(DeleteBehavior.Cascade); 

            builder.HasOne(cq => cq.Shop)
                .WithMany(s => s.CommissionQuotes)
                .HasForeignKey(cq => cq.ShopId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
