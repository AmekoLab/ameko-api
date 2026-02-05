using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Part
{
    public class CheckStockRequest
    {
        public List<Guid> ProductIds { get; set; } = new List<Guid>();
    }
}
