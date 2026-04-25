using System;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Builder
{
    /// <summary>
    /// Item trong batch save options — JSON only, không hỗ trợ upload file.
    /// Dùng ExistingLayerUrl để truyền URL ảnh layer đã có sẵn.
    /// </summary>
    public class BatchKitOptionItem
    {
        public Guid BaseKitId { get; set; }
        public Guid ComponentId { get; set; }
        public string StepName { get; set; } = string.Empty;
        public int StepOrder { get; set; }
        public bool IsDefault { get; set; }
        public string? Tags { get; set; }
        public string? NextStepFilterRule { get; set; }
        public string? ExistingLayerUrl { get; set; }
    }
}
