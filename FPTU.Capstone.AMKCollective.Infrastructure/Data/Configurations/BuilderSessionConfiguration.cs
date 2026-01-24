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
    public class BuilderSessionConfiguration : IEntityTypeConfiguration<BuilderSession>
    {
        public void Configure(EntityTypeBuilder<BuilderSession> builder)
        {
            builder.ToTable("BuilderSessions");

            builder.HasKey(x => x.Id);

            // Cấu hình cột JSON lưu các món đã chọn
            builder.Property(x => x.SelectedItemsJson)
                .IsRequired()
                .HasColumnType("longtext"); // Lưu chuỗi JSON dài thoải mái

            builder.Property(x => x.CurrentStep)
                .HasMaxLength(50) // VD: "case", "switch"
                .IsRequired();

            //Định nghĩa kiểu số thập phân cho tiền tệ
            builder.Property(x => x.TotalPrice)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0);

            builder.Property(x => x.ExpiresAt)
                .IsRequired();

            // --- Relationships ---

            // 1. Liên kết với Base Kit (Model)
            builder.HasOne(x => x.BaseKit)
                .WithMany() // Một Model không cần chứa list các Session tạm
                .HasForeignKey(x => x.BaseKitId)
                .OnDelete(DeleteBehavior.Cascade); // Xóa Kit thì xóa luôn Session rác liên quan

            // 2. Liên kết với User (Optional)
            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull); // Xóa User thì set UserId về null
        }
    }
}