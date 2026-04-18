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

        // === Nhánh hình ảnh tích lũy ===
        /// <summary>Tag nhánh mà option này thuộc về, VD: "case-den". Null = tương thích mọi nhánh.</summary>
        public string? Tags { get; set; }
        /// <summary>Luật lọc bước tiếp theo, VD: "plate:case-den". Null = không lọc.</summary>
        /// muốn 1 linh kiện nhiều nhánh thì bước tiếp theo nhập trùng tag ở nhiều linh kiện tiếp theo là được.
        /// VD bước tiếp theo là plate thì nhập thế này: plate:tag
        /// ở bước plate thì tag sẽ là như thế này: tag
        public string? NextStepFilterRule { get; set; }
    }
}
