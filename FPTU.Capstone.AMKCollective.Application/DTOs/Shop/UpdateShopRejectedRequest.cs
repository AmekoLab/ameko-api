using Microsoft.AspNetCore.Http;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Shop
{
    public class UpdateShopRejectedRequest
    {
        // Các field CRITICAL (chỉ sửa được khi shop bị Rejected)
        public string? ShopName { get; set; }
        public string? CitizenId { get; set; }
        public string? TaxCode { get; set; }
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankAccountName { get; set; }
        
        // Có thể sửa info liên hệ
        public string? Bio { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ContactEmail { get; set; }
        public IFormFile? LogoImage { get; set; }
        public IFormFile? BannerImage { get; set; }
    }
}
