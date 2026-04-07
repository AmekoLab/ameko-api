using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProductFeedback
{
    public class ReplyAssembledProductFeedbackRequest
    {
        [Required(ErrorMessage = "Feedback content cannot be empty.")]
        [MaxLength(2000, ErrorMessage = "Feedback content must not exceed 2000 characters.")]
        public string Reply { get; set; } = string.Empty;
    }
}
