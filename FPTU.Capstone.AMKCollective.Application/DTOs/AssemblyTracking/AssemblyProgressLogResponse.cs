using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.AssemblyTracking
{
    public class AssemblyProgressLogResponse
    {
        public Guid ProgressLogId { get; set; }
        public Guid OrderItemId { get; set; }
        public string StepName { get; set; } = string.Empty;
        public int StepOrder { get; set; }
        public AssemblyStepStatus Status { get; set; }
        public string? Note { get; set; }
        // Trả về cho Frontend xem thì vẫn là string URL
        public string? MediaUrl { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
