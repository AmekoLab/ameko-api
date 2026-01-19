using System;

namespace FPTU.Capstone.AMKCollective.Application.DTOs.Auth
{
    public class LoginResponse
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Role { get; set; } = null!;
        public string Token { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
    }
}
