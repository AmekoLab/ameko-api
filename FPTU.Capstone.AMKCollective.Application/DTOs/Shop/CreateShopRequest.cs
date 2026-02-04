using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Shop
{
    public class CreateShopRequest
    {
        [Required]
        public string ShopName { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public string? ContactEmail { get; set; }
        [Required]
        public string CitizenId { get; set; } = string.Empty;
        public string? TaxCode { get; set; }
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankAccountName { get; set; }
        public IFormFile? LogoImage { get; set; }
        public IFormFile? BannerImage { get; set; }
    }
}
