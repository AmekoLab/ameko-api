using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.AssemblyTracking
{
    public class UpdateAssemblyProgressRequest
    {
        public AssemblyStepStatus Status { get; set; }
        public string? Note { get; set; }
        public IFormFile? MediaFile { get; set; }
    }
}
