using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Payment
{
    public class PaymentResponse
    {
        public Guid Id { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "vnd";
        public string Status { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = "CreditCard";
        public DateTime CreatedAt { get; set; }
    }
}
