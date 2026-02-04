using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Order
{
    public class OrderGroupResponse
    {
        public Guid Id { get; set; }
        public decimal TotalGroupAmount { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public List<OrderResponse> Orders { get; set; } = new();
    }
}
