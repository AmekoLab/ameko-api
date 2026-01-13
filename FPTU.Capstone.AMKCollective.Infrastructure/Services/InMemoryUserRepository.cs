using System;
using System.Collections.Generic;
using FPTU.Capstone.AMKCollective.Application.Interfaces;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    // Simple in-memory repository for development / code-first workflows
    public class InMemoryUserRepository : IUserRepository
    {
        private readonly List<User> _users = new()
        {
            new User { Id = Guid.NewGuid(), Email = "alice@example.com", FullName = "Alice A." },
            new User { Id = Guid.NewGuid(), Email = "bob@example.com", FullName = "Bob B." },
            new User { Id = Guid.NewGuid(), Email = "carol@example.com", FullName = "Carol C." }
        };

        public IEnumerable<User> GetAll() => _users;
    }
}
