using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    public class NextStepResponse
    {
        public BuilderStepDetailResponse Step { get; set; } = new();
        public List<CompatiblePartResponse> Products { get; set; } = new();
    }
}
