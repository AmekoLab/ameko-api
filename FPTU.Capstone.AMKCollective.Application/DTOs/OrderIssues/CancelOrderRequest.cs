using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues
{
    public class CancelOrderRequest
    {
        public Guid OrderId { get; set; }
        public string Reason { get; set; } = string.Empty; 
        public string? Description { get; set; } 
        //public string? EvidenceUrl { get; set; } 
    }
}
