using System;
using System.Collections.Generic;
using System.Linq;
using FPTU.Capstone.AMKCollective.Application.Interfaces;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;

        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public IEnumerable<User> GetAll()
        {
            return _context.Users.Include(u => u.Role).ToList();
        }

        public User? GetByUsername(string username)
        {
            return _context.Users.Include(u => u.Role).FirstOrDefault(u => u.Username == username);
        }

        public User? GetByEmail(string email)
        {
            return _context.Users.Include(u => u.Role).FirstOrDefault(u => u.Email == email);
        }

        public User? GetById(Guid id)
        {
            return _context.Users.Include(u => u.Role).FirstOrDefault(u => u.Id == id);
        }

        public void Update(User user)
        {
            _context.Users.Update(user);
        }

        public void Add(User user)
        {
            _context.Users.Add(user);
        }

        public void Delete(User user)
        {
            _context.Users.Remove(user);
        }

        public Role? GetRoleByName(RoleType roleName)
        {
            return _context.Roles.FirstOrDefault(r => r.Name == roleName);
        }

        public User? GetUserWithRefreshTokens(Guid id)
        {
            return _context.Users.Include(u => u.Role).Include(u => u.RefreshTokens).FirstOrDefault(u => u.Id == id);
        }

        public void AddRefreshToken(RefreshToken token)
        {
            _context.RefreshTokens.Add(token);
        }
    }
}
