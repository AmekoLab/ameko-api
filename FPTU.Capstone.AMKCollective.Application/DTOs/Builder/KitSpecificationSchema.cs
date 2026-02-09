using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    public class KitSpecificationSchema
    {
        // Danh sách các bước theo thứ tự
        public List<KitWorkflowStep> Workflow { get; set; } = new();
    }
}
