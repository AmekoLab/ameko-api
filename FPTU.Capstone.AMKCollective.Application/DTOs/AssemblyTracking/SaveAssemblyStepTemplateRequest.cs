using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.AssemblyTracking
{
    public class SaveAssemblyStepTemplateRequest
    {
        public string StepName { get; set; } = string.Empty;
        public int StepOrder { get; set; }
        public bool IsRequired { get; set; } = true;
    }
}
