using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Data.Configurations
{
    public class OrderGroupConfiguration : IEntityTypeConfiguration<OrderGroup>
    {
        public void Configure(EntityTypeBuilder<OrderGroup> builder)
        {
            builder.ToTable("OrderGroups");
            builder.HasKey(og => og.Id);

            builder.Property(og => og.TotalGroupAmount)
                .HasPrecision(18, 2)
                .IsRequired();
            builder.Property(x => x.PaymentStatus)
                .HasConversion<string>()
                .HasDefaultValue(PaymentStatus.Pending);

           
            builder.HasOne(og => og.Customer)
           .WithMany() 
           .HasForeignKey(og => og.CustomerId)
           .OnDelete(DeleteBehavior.Restrict);

        }
    }
}
