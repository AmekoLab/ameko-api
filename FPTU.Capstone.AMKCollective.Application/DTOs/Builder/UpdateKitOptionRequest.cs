using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    public class UpdateKitOptionRequest
    {
        public string? Tags { get; set; }
        public string? NextStepFilterRule { get; set; }
        public IFormFile? LayerImageFile { get; set; }
    }
}
