using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class AssemblyProgressLog : BaseEntity
    {
        public Guid OrderItemId { get; set; }

        public string StepName { get; set; } = string.Empty;

        public int StepOrder { get; set; }

        public AssemblyStepStatus Status { get; set; } = AssemblyStepStatus.Pending;

        public string? Note { get; set; }

        public string? MediaUrl { get; set; } // Link ảnh hoặc video Cloudinary

        public DateTime? CompletedAt { get; set; }

        // Navigation Properties
        public virtual OrderItem OrderItem { get; set; } = null!;
    }
}
