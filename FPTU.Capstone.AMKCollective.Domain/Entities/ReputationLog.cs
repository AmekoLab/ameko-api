using System;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class ReputationLog : BaseEntityInt
    {
        public string TargetType { get; set; } = string.Empty;
        public Guid TargetId { get; set; }
        public int Delta { get; set; }
        public int ScoreAfter { get; set; }
        public string? Reason { get; set; }
        public string? ReferenceType { get; set; }
        public string? ReferenceId { get; set; }
    }
}
