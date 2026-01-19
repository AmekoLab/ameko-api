using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class KitDesignOption : BaseEntity
    {
        public Guid BaseKitId { get; set; }
        public Guid ComponentId { get; set; }
        public string? LayerImageUrl { get; set; }
        public string? StepName { get; set; }
        public int StepOrder { get; set; }
        public bool IsDefault { get; set; } = false;

        // Navigation Properties
        public virtual Model BaseKit { get; set; } = null!;
        public virtual Model Component { get; set; } = null!;
    }
}
