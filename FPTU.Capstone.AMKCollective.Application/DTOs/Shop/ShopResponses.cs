using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Shop
{
    /// <summary>
    /// DTO thông tin shop cơ bản
    /// </summary>
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

    /// <summary>
    /// DTO thông tin shop chi tiết (cho admin/owner)
    /// </summary>
    public class ShopDetailDto : ShopDto
    {
        public Guid UserId { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ContactEmail { get; set; }

        // KYC
        public string? CitizenId { get; set; }
        public string? TaxCode { get; set; }

        // Status
        public ShopStatus Status { get; set; }
        public bool IsActive { get; set; }
        public string? AdminNote { get; set; }

        // Tài chính
        public decimal TotalRevenue { get; set; }
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankAccountName { get; set; }
    }
}
