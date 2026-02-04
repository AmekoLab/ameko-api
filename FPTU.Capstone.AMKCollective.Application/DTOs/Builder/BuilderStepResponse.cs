using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    public class BuilderStepResponse
    {
        public string Message { get; set; } = "Product selected";
        public BuilderSessionData Data { get; set; } = new();
    }
}
