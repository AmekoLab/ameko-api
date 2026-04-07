using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProductFeedback
{
    public class AssembledProductFeedbackEligibilityItem
    {
        public Guid OrderId { get; set; }
        public Guid OrderItemId { get; set; }
        public DateTime OrderCreatedAt { get; set; }
        public bool HasFeedback { get; set; }
        public bool CanReview { get; set; }
        public Guid? FeedbackId { get; set; }
    }
}
