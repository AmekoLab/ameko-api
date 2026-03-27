using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    public class BuilderAddonRequest
    {
        public Guid SessionId { get; set; }
        public List<BuilderAddonItem> Items { get; set; } = new();
    }
}
