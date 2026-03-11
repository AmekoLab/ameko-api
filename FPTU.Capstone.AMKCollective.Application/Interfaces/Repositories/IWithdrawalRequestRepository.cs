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
        Task AddAsync(WithdrawalRequest request);
        void Update(WithdrawalRequest request);
    }
}
