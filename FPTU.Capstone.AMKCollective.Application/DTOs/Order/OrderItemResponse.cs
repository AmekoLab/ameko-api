using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Order
{
    public class OrderItemResponse
    {
        public Guid Id { get; set; }
        public Guid? ProductId { get; set; }
        public Guid? AssembledProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductImage { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public bool IsCustom { get; set; }
        public string? Note { get; set; }
        public List<Guid>? CustomComponentIds { get; set; }
        public List<OrderItemComponentDto> OrderItemComponents { get; set; } = new();
    }
}
