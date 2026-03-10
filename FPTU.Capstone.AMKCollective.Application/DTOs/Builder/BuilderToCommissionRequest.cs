using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    public class BuilderToCommissionRequest
    {
        public Guid SessionId { get; set; }
        public string CustomerNote { get; set; } = string.Empty;
    }
}
