using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IWithdrawalRequestRepository
    {
        Task<WithdrawalRequest?> GetByIdAsync(Guid id);
        Task<List<WithdrawalRequest>> GetByUserIdAsync(Guid userId);
        Task<List<WithdrawalRequest>> GetPendingAsync();
        
        Task<(List<WithdrawalRequest> Items, int TotalCount)> GetByUserIdPagedAsync(Guid userId, int pageIndex, int pageSize);
        Task<(List<WithdrawalRequest> Items, int TotalCount)> GetPendingPagedAsync(int pageIndex, int pageSize, string? shopName = null);
        Task<(List<WithdrawalRequest> Items, int TotalCount)> GetProcessedPagedAsync(int pageIndex, int pageSize);
        
        Task AddAsync(WithdrawalRequest request);
        void Update(WithdrawalRequest request);
    }
}
