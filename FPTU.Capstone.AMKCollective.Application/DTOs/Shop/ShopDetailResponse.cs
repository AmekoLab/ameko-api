using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Shop
{
    public class ShopDetailResponse : ShopResponse
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
        public int ResubmitCount { get; set; }
        public DateTime? LastResubmitTime { get; set; }
        public int RemainingResubmits { get; set; }
    }
}
