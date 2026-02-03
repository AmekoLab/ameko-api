using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.User
{
    /// <summary>
    /// DTO for User list/pagination responses.
    /// </summary>
    public class UserResponse
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string FullName => $"{FirstName} {LastName}";
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public Gender? Gender { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Image { get; set; }
        public bool EmailConfirmed { get; set; }
        public AccountStatus Status { get; set; }
        public string? RoleName { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
