using FPTU.Capstone.AMKCollective.Application.DTOs.Reputation;
using System;
using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Reputation
{
    public class AdminAdjustReputationDto
    {
        [Required]
        [EnumDataType(typeof(ReputationTargetType))]
        public ReputationTargetType TargetType { get; set; }       
        public Guid TargetId { get; set; }

        [Range(-100, 100, ErrorMessage = "Delta must be between -100 and 100.")]
        public int Delta { get; set; }

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }
}
