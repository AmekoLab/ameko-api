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
        public string? UserId { get; set; }
        public string? Bio { get; set; }
        public string? LogoUrl { get; set; }
        public string? BannerUrl { get; set; }
        public double Rating { get; set; }
        public int TotalSales { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public int FollowersCount { get; set; }
        public int FollowingCount { get; set; }
    }
}
