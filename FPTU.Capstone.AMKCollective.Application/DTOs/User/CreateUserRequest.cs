using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.User
{
    public class CreateUserRequest
    {
        [Required]
        public string Username { get; set; } = null!;
        [Required]
        public string Password { get; set; } = null!;
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;
        [Required]
        public string FirstName { get; set; } = null!;
        [Required]
        public string LastName { get; set; } = null!;
        
        public RoleType Role { get; set; } = RoleType.Customer;
        public AccountStatus Status { get; set; } = AccountStatus.Active;
        
        public Gender? Gender { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
