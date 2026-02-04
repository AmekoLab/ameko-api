using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Shop
{
    public class ApproveShopRequest
    {
        public ShopStatus Status { get; set; }
        public string? AdminNote { get; set; }
    }
}
