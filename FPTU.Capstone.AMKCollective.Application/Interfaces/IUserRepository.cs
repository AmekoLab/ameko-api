using System;
using System.Collections.Generic;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces
{
    public interface IUserRepository
    {
        IEnumerable<User> GetAll();
        User? GetByUsername(string username);
        User? GetByEmail(string email);
        User? GetById(Guid id);
        void Update(User user);
        void Add(User user);
        void Delete(User user);
        Role? GetRoleByName(RoleType roleName);
        User? GetUserWithRefreshTokens(Guid id);
        void AddRefreshToken(RefreshToken token);
    }
}
