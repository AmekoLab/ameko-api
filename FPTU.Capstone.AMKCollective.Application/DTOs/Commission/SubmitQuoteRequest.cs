using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Commission
{
    public class SubmitQuoteRequest
    {
        [Required]
        [Range(1000, double.MaxValue, ErrorMessage = "Quoted price must exceed 1,000 VND")]
        public decimal QuotedPrice { get; set; }

        [Required]
        [Range(1, 365, ErrorMessage = "Completion time must be within the range of 1 to 365 days.")]
        public int EstimatedDays { get; set; }

        public string? ShopNotes { get; set; }
    }
}
