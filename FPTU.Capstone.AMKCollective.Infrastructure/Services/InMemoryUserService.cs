using System;
using System.Collections.Generic;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.Interfaces;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    // Simple in-memory implementation of IUserService for demo and wiring purposes
    public class InMemoryUserService : IUserService
    {
        private readonly List<UserDto> _users = new()
        {
            new UserDto(Guid.NewGuid(), "alice@example.com", "Alice A."),
            new UserDto(Guid.NewGuid(), "bob@example.com", "Bob B."),
            new UserDto(Guid.NewGuid(), "carol@example.com", "Carol C.")
        };

        public IEnumerable<UserDto> GetAll() => _users;
    }
}
