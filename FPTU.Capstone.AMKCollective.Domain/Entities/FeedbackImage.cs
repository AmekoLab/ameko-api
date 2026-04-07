using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Domain.Entities
{
    public class FeedbackImage : BaseEntity
    {
        public Guid? FeedbackId { get; set; }
        public Guid? AssembledProductFeedbackId { get; set; }

        [Required]
        [MaxLength(500)]
        public string ImageUrl { get; set; } = string.Empty;

        // Navigation Property
        public virtual Feedback? Feedback { get; set; }
        public virtual AssembledProductFeedback? AssembledProductFeedback { get; set; }
    }
}
