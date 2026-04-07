using System;
using System.Collections.Generic;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProductFeedback
{
    public class AssembledProductFeedbackEligibilityResponse
    {
        public Guid ProductId { get; set; }
        public List<AssembledProductFeedbackEligibilityItem> Items { get; set; } = new List<AssembledProductFeedbackEligibilityItem>();
    }
}
