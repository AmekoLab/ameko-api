using FPTU.Capstone.AMKCollective.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Shop
{
    /// <summary>
    /// Request đăng ký shop mới (form-data)
    /// </summary>
    public class CreateShopRequest
    {
        [Required]
        public string ShopName { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ContactEmail { get; set; }

        // KYC - bắt buộc
        [Required]
        public string CitizenId { get; set; } = string.Empty;
        public string? TaxCode { get; set; }

        // Thông tin ngân hàng
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankAccountName { get; set; }

        // Hình ảnh
        public IFormFile? LogoImage { get; set; }
        public IFormFile? BannerImage { get; set; }
    }

    /// <summary>
    /// Request cập nhật thông tin shop (form-data)
    /// </summary>
    public class UpdateShopRequest
    {
        public string? Bio { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ContactEmail { get; set; }
        public bool? IsActive { get; set; }

        // Thông tin ngân hàng
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankAccountName { get; set; }

        // Hình ảnh
        public IFormFile? LogoImage { get; set; }
        public IFormFile? BannerImage { get; set; }
    }

    /// <summary>
    /// Request Admin duyệt/từ chối shop
    /// </summary>
    public class ApproveShopRequest
    {
        public ShopStatus Status { get; set; }
        public string? AdminNote { get; set; }
    }
}
