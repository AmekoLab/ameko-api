using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues
{
    public class ProcessIssueRequest
    {
        [Required]
        public Guid IssueId { get; set; }

        [Required]
        public OrderIssueStatus Decision { get; set; } // Approved, Rejected, Cancelled (nếu system timeout)

        public string? ShopResponse { get; set; } // Lời nhắn từ Shop
    }
}
