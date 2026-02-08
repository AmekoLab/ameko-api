using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Shop
{
    public class UpdateShopRequest
    {
        public string? ShopName { get; set; }
        public string? Bio { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ContactEmail { get; set; }
        // IsActive đã được tách ra endpoint riêng (Deactivate/Reactivate) — shop owner quyết định
        // Status chỉ admin mới được thay đổi
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankAccountName { get; set; }
        public IFormFile? LogoImage { get; set; }
        public IFormFile? BannerImage { get; set; }
    }
}
