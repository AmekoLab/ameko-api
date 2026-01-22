using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.User
{
    public class UpdateProfileRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public Gender? Gender { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Image { get; set; }
        
        // Artisan info
        public string? StoreAddress { get; set; }
        public string? Banner { get; set; }
        public string? StoreDescription { get; set; }
    }
}
