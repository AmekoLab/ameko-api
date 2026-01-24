using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs
{
    public class ShopDto
    {
        public Guid Id { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? LogoUrl { get; set; }
        public string? BannerUrl { get; set; }
        public double Rating { get; set; }
        public int TotalSales { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ShopDetailDto : ShopDto
    {
        public Guid UserId { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ContactEmail { get; set; }

        public string? CitizenId { get; set; }
        public string? TaxCode { get; set; }

        public ShopStatus Status { get; set; }
        public bool IsActive { get; set; }
        public string? AdminNote { get; set; }

        public decimal TotalRevenue { get; set; }
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankAccountName { get; set; }
    }

    // --- REQUEST DTOs (Application Layer) ---

    public class CreateShopRequest
    {
        public string ShopName { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ContactEmail { get; set; }

        // KYC is required
        public string CitizenId { get; set; } = string.Empty;
        public string? TaxCode { get; set; }

        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankAccountName { get; set; }

        public Stream? LogoStream { get; set; }
        public string? LogoFileName { get; set; }

        public Stream? BannerStream { get; set; }
        public string? BannerFileName { get; set; }
    }

    public class UpdateShopProfileRequest
    {
        public string? Bio { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ContactEmail { get; set; }

        public bool? IsActive { get; set; } 

        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankAccountName { get; set; }

        // Images
        public Stream? LogoStream { get; set; }
        public string? LogoFileName { get; set; }

        public Stream? BannerStream { get; set; }
        public string? BannerFileName { get; set; }
    }

    // 3. Admin Approve/Reject
    public class ApproveShopRequest
    {
        public ShopStatus Status { get; set; } // Active or Rejected
        public string? AdminNote { get; set; }
    }
}
