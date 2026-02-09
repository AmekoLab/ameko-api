using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    public class BuilderSessionData
    {
        public SessionInfoResponse Session { get; set; } = new();
        public NextStepResponse NextStep { get; set; } = new();
        //Danh sách các bước (VD: ["case", "cable", "switch"...])
        public List<string> WorkflowSteps { get; set; } = new();
    }
}
