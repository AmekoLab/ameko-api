using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IOrderGroupRepository
    {
        Task<OrderGroup?> GetByIdAsync(Guid id, CancellationToken token = default);

        Task CreateAsync(OrderGroup orderGroup, CancellationToken token = default);

        Task UpdatePaymentStatusAsync(Guid orderGroupId, PaymentStatus status, CancellationToken token = default);
        Task<IEnumerable<OrderGroup>> GetByUserIdAsync(Guid userId, CancellationToken token = default);

        Task<int> SaveChangesAsync(CancellationToken token = default);
        Task UpdateAsync(OrderGroup orderGroup);
    }
}
