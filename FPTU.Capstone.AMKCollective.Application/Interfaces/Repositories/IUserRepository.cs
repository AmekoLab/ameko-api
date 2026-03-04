using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IUserRepository
    {
        Task<(IEnumerable<User> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize);
        Task<User?> GetByUsernameAsync(string username);
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByIdAsync(Guid id);
        Task UpdateAsync(User user);
        Task AddAsync(User user);
        Task DeleteAsync(User user);
        Task<Role?> GetRoleByNameAsync(RoleType roleName);
        Task<User?> GetUserWithRefreshTokensAsync(Guid id);
        Task<(IEnumerable<User> Items, int TotalCount)> SearchByNamePagedAsync(string name, int pageNumber, int pageSize);
        Task AddRefreshTokenAsync(RefreshToken token);
        Task RemoveAllRefreshTokensAsync(Guid userId);
    }
}
