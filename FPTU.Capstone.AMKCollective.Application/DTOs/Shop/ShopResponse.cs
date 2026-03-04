using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Shop
{
    public class ShopResponse
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
}
