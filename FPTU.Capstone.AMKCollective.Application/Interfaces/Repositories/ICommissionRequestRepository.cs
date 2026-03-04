using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface ICommissionRequestRepository
    {
        Task<CommissionRequest?> GetByIdAsync(Guid id);
        Task<IEnumerable<CommissionRequest>> GetByUserIdAsync(Guid userId);
        Task<IEnumerable<CommissionRequest>> GetOpenPoolRequestsAsync();
        Task<IEnumerable<CommissionRequest>> GetTargetedRequestsForShopAsync(Guid shopId);
        Task AddAsync(CommissionRequest request);
        Task UpdateAsync(CommissionRequest request);
    }
}
