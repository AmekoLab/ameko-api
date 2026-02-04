using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Order
{
    public class UpdateCartItemRequest
    {
        public Guid OrderItemId { get; set; }
        public int Quantity { get; set; }
    }
}
