using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Feedback
{
    public class AssembledItemFeedbackEligibility
    {
        public Guid OrderItemId { get; set; }
        public Guid AssembledProductId { get; set; }
        public bool CanReview { get; set; }
        public bool HasFeedback { get; set; }
        public Guid? FeedbackId { get; set; }
    }
}
