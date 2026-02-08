using Microsoft.AspNetCore.Http;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Shop
{
    public class PatchShopRequest
    {
        // Các field NON-CRITICAL (khách hàng có thể thay đổi bất cứ lúc nào)
        public string? Bio { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ContactEmail { get; set; }
        public IFormFile? LogoImage { get; set; }
        public IFormFile? BannerImage { get; set; }
    }
}
