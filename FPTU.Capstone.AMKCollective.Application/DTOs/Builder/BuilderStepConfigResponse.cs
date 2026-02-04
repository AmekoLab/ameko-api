using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    public class BuilderStepConfigResponse
    {
        public string StepName { get; set; } = string.Empty;
        public int StepOrder { get; set; }
        public string PartType { get; set; } = string.Empty;
        public bool IsRequired { get; set; } = true;
        public List<CompatiblePartResponse> Options { get; set; } = new List<CompatiblePartResponse>();
    }
}
