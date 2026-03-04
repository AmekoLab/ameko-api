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
    public class CommissionRequestConfiguration : IEntityTypeConfiguration<CommissionRequest>
    {
        public void Configure(EntityTypeBuilder<CommissionRequest> builder)
        {
            builder.ToTable("CommissionRequests");

            builder.HasOne(cr => cr.User)
                .WithMany(u => u.CommissionRequests) // Nếu User không có ICollection<CommissionRequest>
                .HasForeignKey(cr => cr.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(cr => cr.TargetedShop)
                .WithMany(s => s.TargetedCommissionRequests)
                .HasForeignKey(cr => cr.TargetedShopId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
