using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Withdrawal
{
    public class WithdrawalRequestResponseDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        
        // Optional user details (e.g. for Admin lists)
        public string? Username { get; set; }
        public string? ShopName { get; set; }

        public decimal Amount { get; set; }

        public string BankName { get; set; } = null!;
        public string BankAccountNumber { get; set; } = null!;
        public string BankAccountName { get; set; } = null!;

        public string Status { get; set; } = null!; // String representation of WithdrawalStatus

        public Guid? AdminId { get; set; }
        public string? AdminMessage { get; set; }
        public string? EvidenceUrl { get; set; }

        public DateTime RequestedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }
}
