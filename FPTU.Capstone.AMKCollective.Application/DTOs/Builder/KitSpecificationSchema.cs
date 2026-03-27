using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    public class KitSpecificationSchema
    {
        // LayoutCode để truyền xuống cho FE vẽ bàn phím ảo
        public string LayoutCode { get; set; } = "layout_default";
        // Danh sách các bước theo thứ tự
        public List<KitWorkflowStep> Workflow { get; set; } = new();
    }
}
