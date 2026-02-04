using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    public class CreateKitOptionRequest
    {
        public Guid BaseKitId { get; set; }
        public Guid ComponentId { get; set; }
        public string StepName { get; set; } = string.Empty;
        public int StepOrder { get; set; }
        public bool IsDefault { get; set; }
        public IFormFile? LayerImageFile { get; set; }
    }
}
