using System;
using System.Collections.Generic;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Feedback
{
    public class FeedbackEligibilityResponse
    {
        public Guid OrderId { get; set; }
        public bool IsOrderCompleted { get; set; }
        public bool CanReviewShop { get; set; }
        public bool HasShopFeedback { get; set; }
        public List<AssembledItemFeedbackEligibility> AssembledItems { get; set; } = new List<AssembledItemFeedbackEligibility>();
    }
}
