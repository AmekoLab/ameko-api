using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Order
{
    public class RepayRequest
    {
        [JsonIgnore]
        public Guid OrderGroupId { get; set; }
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CreditCard;
        /// <summary>Required when PaymentMethod == Wallet.</summary>
        public string? WalletPin { get; set; }
        public string SuccessUrl { get; set; } = string.Empty;
        public string CancelUrl { get; set; } = string.Empty;
    }
}
