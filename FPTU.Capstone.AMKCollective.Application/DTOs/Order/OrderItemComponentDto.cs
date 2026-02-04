using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Order
{
    public class OrderItemComponentDto
    {
        public Guid PartId { get; set; }
        public string PartName { get; set; } = string.Empty;
        public decimal PartPriceSnapshot { get; set; }
        public string PartImageUrl { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }
}
