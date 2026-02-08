using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IPaymentRepository
    {
        Task AddAsync(Payment payment, CancellationToken token = default);
        Task<Payment?> GetByStripeSessionIdAsync(string sessionId, CancellationToken token = default);
        Task<Payment?> GetByStripePaymentIntentIdAsync(string paymentIntentId,  CancellationToken token = default);
        Task<IEnumerable<Payment>> GetByOrderGroupIdAsync(Guid orderGroupId, CancellationToken token = default);

        Task<int> SaveChangesAsync(CancellationToken token = default);

        Task<Payment?> GetPaymentByOrderGroupIdAsync(Guid orderGroupId);
        Task<IEnumerable<Payment>> GetByUserIdAsync(Guid userId, CancellationToken token = default);

    }
}
