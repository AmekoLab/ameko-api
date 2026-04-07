using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class CommissionQuoteRepository : ICommissionQuoteRepository
    {
        private readonly ApplicationDbContext _context;

        public CommissionQuoteRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<CommissionQuote?> GetByIdAsync(Guid id)
        {
            return await _context.CommissionQuotes
                .Include(q => q.CommissionRequest)
                .Include(q => q.Shop)
                .FirstOrDefaultAsync(q => q.Id == id);
        }

        public async Task<IEnumerable<CommissionQuote>> GetByRequestIdAsync(Guid requestId)
        {
            return await _context.CommissionQuotes
                .Where(q => q.CommissionRequestId == requestId)
                .Include(q => q.Shop)
                .OrderBy(q => q.QuotedPrice) 
                .ToListAsync();
        }

        public async Task AddAsync(CommissionQuote quote)
        {
            await _context.CommissionQuotes.AddAsync(quote);
        }

        public async Task UpdateAsync(CommissionQuote quote)
        {
            _context.CommissionQuotes.Update(quote);
            await Task.CompletedTask;
        }

        public async Task<IEnumerable<CommissionQuote>> GetQuotesByShopIdAsync(Guid shopId)
        {
            return await _context.CommissionQuotes
                .Include(q => q.CommissionRequest)
                .Where(q => q.ShopId == shopId)
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<CommissionQuote>> GetExpiredCustomerDecisionQuotesAsync(DateTime now)
        {
            return await _context.CommissionQuotes
                .Include(q => q.Shop)
                .Include(q => q.CommissionRequest)
                    .ThenInclude(r => r.Quotes)
                        .ThenInclude(rq => rq.Shop)
                .Where(q => q.Status == Domain.Enums.QuoteStatus.PendingUserDecision
                    && q.CustomerDecisionDeadlineAt.HasValue
                    && q.CustomerDecisionDeadlineAt.Value <= now)
                .ToListAsync();
        }

        /// <summary>
        /// Lấy các quote có Status=Accepted và UpdatedAt ≤ acceptedBefore.
        /// acceptedBefore = now - PaymentWindowHours (ví dụ: now - 48h).
        /// Lưu ý: UpdatedAt chỉ được set 1 lần khi AcceptQuoteAsync (không thả touch sau đó).
        /// </summary>
        public async Task<List<CommissionQuote>> GetAcceptedQuotesPastPaymentDeadlineAsync(DateTime acceptedBefore)
        {
            return await _context.CommissionQuotes
                .Include(q => q.Shop)
                .Include(q => q.CommissionRequest)
                    .ThenInclude(r => r.Quotes)
                        .ThenInclude(rq => rq.Shop)
                .Where(q =>
                    q.Status == Domain.Enums.QuoteStatus.Accepted
                    && q.UpdatedAt.HasValue
                    && q.UpdatedAt.Value <= acceptedBefore)
                .ToListAsync();
        }
    }
}
