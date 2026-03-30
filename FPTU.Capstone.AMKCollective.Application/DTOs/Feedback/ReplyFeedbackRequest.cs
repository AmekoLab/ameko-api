using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Feedback
{
    public class ReplyFeedbackRequest
    {
        [Required(ErrorMessage = "Feedback content cannot be empty.")]
        [MaxLength(2000, ErrorMessage = "Feedback content must not exceed 2000 characters.")]
        public string Reply { get; set; } = string.Empty;
    }
}
