using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Order
{
    public class CheckoutResponse
    {
        public Guid OrderGroupId { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentUrl { get; set; } = string.Empty;
    }
}
