using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Order
{
    public class AddToCartRequest
    {
        public Guid? ProductId { get; set; }
        public int Quantity { get; set; }
        public bool IsCustom { get; set; } = false;
        //public List<Guid>? CustomComponentIds { get; set; }
        public Guid? BuilderSessionId { get; set; }
    }
}
