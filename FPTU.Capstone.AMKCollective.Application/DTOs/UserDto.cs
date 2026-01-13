using System;

namespace FPTU.Capstone.AMKCollective.Application.DTOs
{
    public record UserDto(Guid Id, string Email, string FullName);
}
