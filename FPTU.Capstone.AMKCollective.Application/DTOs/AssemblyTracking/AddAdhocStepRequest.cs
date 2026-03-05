using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.AssemblyTracking
{
    public class AddAdhocStepRequest
    {
        public string StepName { get; set; } = string.Empty;
        public int StepOrder { get; set; }
        public string? Note { get; set; }
        public IFormFile? MediaFile { get; set; }
    }
}
