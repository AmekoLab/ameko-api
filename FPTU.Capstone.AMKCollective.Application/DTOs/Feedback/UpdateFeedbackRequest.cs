using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Feedback
{
    public class UpdateFeedbackRequest
    {
        [Required]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
        public int Rating { get; set; }

        [MaxLength(2000, ErrorMessage = "Review content must not exceed 2000 characters.")]
        public string? Comment { get; set; }

        [MaxLength(5, ErrorMessage = "You can upload a maximum of 5 images.")]
        public List<IFormFile>? Images { get; set; }
    }
}
