using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues
{
    public class OrderIssueLogResponse
    {
        public Guid Id { get; set; }
        public Guid OrderIssueId { get; set; }
        public Guid ActionById { get; set; }
        public RoleType ActionByRole { get; set; } // Trả về Customer, Shop hay Admin
        public OrderIssueAction Action { get; set; }
        public string Comment { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
