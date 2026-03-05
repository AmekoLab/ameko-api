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
        public Guid ActorId { get; set; }
        public RoleType ActorRole { get; set; }
        public string? ActorRoleName { get; set; }
        public OrderIssueAction ActionType { get; set; }
        public string? ActionName { get; set; }
        public string Comment { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
