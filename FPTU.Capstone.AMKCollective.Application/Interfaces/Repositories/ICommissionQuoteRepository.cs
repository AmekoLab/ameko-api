using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface ICommissionQuoteRepository
    {
        Task<CommissionQuote?> GetByIdAsync(Guid id);
        Task<IEnumerable<CommissionQuote>> GetByRequestIdAsync(Guid requestId);
        Task<IEnumerable<CommissionQuote>> GetQuotesByShopIdAsync(Guid shopId);
        Task<List<CommissionQuote>> GetExpiredCustomerDecisionQuotesAsync(DateTime now);
        /// <summary>
        /// Lấy tất cả quote đã Accept (UpdatedAt ≤ acceptedBefore) nhưng user chưa thanh toán.
        /// acceptedBefore = DateTime.UtcNow - PaymentWindowHours
        /// </summary>
        Task<List<CommissionQuote>> GetAcceptedQuotesPastPaymentDeadlineAsync(DateTime acceptedBefore);
        Task AddAsync(CommissionQuote quote);
        Task UpdateAsync(CommissionQuote quote);
        Task<HashSet<Guid>> GetRequestIdsWithPendingQuoteByShopAsync(Guid shopId, List<Guid> requestIds);

    }
}
