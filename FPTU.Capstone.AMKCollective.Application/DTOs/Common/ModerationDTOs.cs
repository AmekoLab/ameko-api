using System;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Common;

public class ModerationResult
{
    public string Severity { get; set; } = "LOW"; // LOW, MEDIUM, HIGH
    public string Reason { get; set; } = string.Empty;
    public string? Suggestion { get; set; }
    
    public bool IsBlocked => Severity == "HIGH";
    public bool RequiresEditing => Severity == "MEDIUM";
}
