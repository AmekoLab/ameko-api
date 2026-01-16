using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class Payment : BaseEntity
    {
        public Guid OrderGroupId { get; set; }
        public string? AddressLine { get; set; }
        public string? WardName { get; set; }

        // Navigation Properties
        public virtual OrderGroup OrderGroup { get; set; } = null!;
    }
}
