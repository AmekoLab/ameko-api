using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    public class BuilderAddonItem
    {
        public Guid ComponentId { get; set; }
        public int Quantity { get; set; } = 1;
        public string PositionNote { get; set; } = string.Empty;
    }
}
