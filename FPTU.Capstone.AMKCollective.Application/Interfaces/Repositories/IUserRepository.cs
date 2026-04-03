using System;
using System.Collections.Generic;
using System.Threading;
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
        Task AddRefreshTokenAsync(RefreshToken token);
        Task RemoveAllRefreshTokensAsync(Guid userId);
        Task<(IEnumerable<User> Items, int TotalCount)> SearchByNamePagedAsync(string name, int pageNumber, int pageSize, CancellationToken token = default);

        /// <summary>
        /// Returns users created within a date range for dashboard analytics.
        /// </summary>
        Task<List<User>> GetUsersForDashboardAsync(DateTime fromUtc, DateTime toUtc, CancellationToken token = default);

        /// <summary>
        /// Deletes accounts that are still pending verification after a configured threshold.
        /// Returns the number of deleted users.
        /// </summary>
        Task<int> DeleteUnverifiedAccountsOlderThanAsync(DateTime thresholdUtc, CancellationToken token = default);
    }
}
