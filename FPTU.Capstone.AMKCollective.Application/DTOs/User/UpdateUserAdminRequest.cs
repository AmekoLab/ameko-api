using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.User
{
    public class UpdateUserAdminRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public AccountStatus? Status { get; set; }
        public RoleType? Role { get; set; }
        
        public Gender? Gender { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public string? PhoneNumber { get; set; }
        public bool? EmailConfirmed { get; set; }
        public bool? PhoneNumberConfirmed { get; set; }
    }
}
