using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class ShopProfile : BaseEntity
    {
        public Guid UserId { get; set; }
        public string ShopName { get; set; } = string.Empty;

        public string? Bio { get; set; }
        public string? Address { get; set; }
        [MaxLength(20)]
        public string? PhoneNumber { get; set; }
        [MaxLength(255)]
        public string? ContactEmail { get; set; }
        [MaxLength(500)]
        public string? LogoUrl { get; set; }
        [MaxLength(500)]
        public string? BannerUrl { get; set; }

        [MaxLength(50)]
        public string? CitizenId { get; set; } //CCCD/CMND
        [MaxLength(50)]
        public string? TaxCode { get; set; }
        public ShopStatus Status { get; set; } = ShopStatus.PendingApproval;
        public string? AdminNote {  get; set; }

        public bool IsActive { get; set; } = true;
        
        public double Rating { get; set; } = 0;
        public int TotalSales { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalRevenue { get; set; } = 0;

        //
        [MaxLength(50)]
        public string? BankName { get; set; } 

        [MaxLength(50)]
        public string? BankAccountNumber { get; set; } 

        [MaxLength(100)]
        public string? BankAccountName { get; set; }

        public int ResubmitCount { get; set; } = 0;
        public DateTime? LastResubmitTime { get; set; }

        // Navigation Properties
        public virtual User User { get; set; } = null!;
        public virtual ICollection<Model> Models { get; set; } = new List<Model>();
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

        public virtual ICollection<CommissionRequest> TargetedCommissionRequests { get; set; } = new List<CommissionRequest>();
        public virtual ICollection<CommissionQuote> CommissionQuotes { get; set; } = new List<CommissionQuote>();
    }
}
