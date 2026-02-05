using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Order
{
    public class CheckoutItemRequest
    {
        public Guid ProductId { get; set; }
        public Guid ShopId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public bool IsCustom { get; set; }
        public List<Guid>? CustomComponentIds { get; set; }
    }
}
