using System;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Reputation
{
    public class ReputationLogDto
    {
        public int Id { get; set; }
        public string TargetType { get; set; } = string.Empty;
        public Guid TargetId { get; set; }
        public int Delta { get; set; }
        public int ScoreAfter { get; set; }
        public string? Reason { get; set; }
        public string? ReferenceType { get; set; }
        public string? ReferenceId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
