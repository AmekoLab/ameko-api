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
    public class UserSearchHistoryConfiguration : IEntityTypeConfiguration<UserSearchHistory>
    {
        public void Configure(EntityTypeBuilder<UserSearchHistory> builder)
        {
            builder.ToTable("UserSearchHistories");
            builder.Property(x => x.Keyword).IsRequired().HasMaxLength(255);
            builder.Property(x => x.SearchType).IsRequired().HasMaxLength(50);

            // Index gộp, để đảm bảo tính Uniqe cho (User + Keyword + Type)
            builder.HasIndex(x => new { x.UserId, x.Keyword, x.SearchType }).IsUnique();

            // Index để sort nhanh khi lấy Feed
            builder.HasIndex(x => x.LastSearchedAt);
            builder.HasIndex(x => x.SearchCount);

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
