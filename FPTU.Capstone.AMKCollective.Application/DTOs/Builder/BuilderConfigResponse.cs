using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    public class BuilderConfigResponse
    {
        public Guid BaseKitId { get; set; }
        public string BaseKitName { get; set; } = string.Empty;
        public string BaseThumbnail { get; set; } = string.Empty;
        public List<BuilderStepConfigResponse> Steps { get; set; } = new List<BuilderStepConfigResponse>();
    }
}
