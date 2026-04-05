using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Order
{
    public class CheckoutRequest
    {
        [Required]
        public string ReceiverName { get; set; } = string.Empty;
        [Required]
        public string ReceiverPhone { get; set; } = string.Empty;
        [Required]
        public string ShippingAddress { get; set; } = string.Empty;
        public string? Note { get; set; }
        public List<CheckoutItemRequest> Items { get; set; } = new();
        public string SuccessUrl { get; set; } = string.Empty;
        public string CancelUrl { get; set; } = string.Empty;
        public List<Guid> SelectedOrderItemIds { get; set; } = new List<Guid>();
        public string? AppliedSystemVoucherCode { get; set; }
        public Dictionary<Guid, string>? AppliedShopVoucherCodes { get; set; }
        public Dictionary<Guid, List<string>>? AppliedShopVoucherCodeGroups { get; set; }
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CreditCard;
    }
}
