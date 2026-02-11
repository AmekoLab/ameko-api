using FPTU.Capstone.AMKCollective.Application.DTOs.Payment;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class PaymentRepository :IPaymentRepository
    {
        private readonly ApplicationDbContext _context;

        public PaymentRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Payment payment, CancellationToken token = default)
        {
            await _context.Payments.AddAsync(payment, token);
        }

        public void Update(Payment payment)
        {
            _context.Payments.Update(payment);
        }

        public async Task<Payment?> GetByStripeSessionIdAsync(string sessionId, CancellationToken token = default)
        {
            return await _context.Payments
                .Include(p => p.OrderGroup)
                .ThenInclude(og => og.Orders)
                .FirstOrDefaultAsync(p => p.StripeSessionId == sessionId, token);
        }
        public async Task<Payment?> GetByStripePaymentIntentIdAsync(string paymentIntentId, CancellationToken token = default)
        {
            return await _context.Payments
                .Include(p => p.OrderGroup)
                .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId, token);
        }
        public async Task<IEnumerable<Payment>> GetByOrderGroupIdAsync(Guid orderGroupId, CancellationToken token = default)
        {
            return await _context.Payments
                .Where(p => p.OrderGroupId == orderGroupId)
                .OrderByDescending(p => p.CreatedAt) 
                .ToListAsync(token);
        }

        public async Task<int> SaveChangesAsync(CancellationToken token = default)
        {
            return await _context.SaveChangesAsync(token);
        }

        public async Task<Payment?> GetPaymentByOrderGroupIdAsync(Guid orderGroupId)
        {
            return await _context.Payments
                .Where(p => p.OrderGroupId == orderGroupId && p.Status == Domain.Enums.PaymentStatus.Paid)
                .OrderByDescending(p => p.CreatedAt) 
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Payment>> GetByUserIdAsync(Guid userId, CancellationToken token = default)
        {
            return await _context.Payments
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt) 
                .ToListAsync(token);
        }

        public async Task<Payment?> GetByIdAsync(Guid id)
        {
            return await _context.Payments
                .Include(p => p.User) 
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<(IEnumerable<Payment> Items, int TotalCount)> GetPaymentsByFilterAsync(PaymentFilterRequest filter)
        {
            var query = _context.Payments.AsQueryable();

            // 1. Filter by User
            if (filter.UserId.HasValue)
            {
                query = query.Where(p => p.UserId == filter.UserId.Value);
            }

            // 2. Filter by Type (Ví dụ: Chỉ lấy Withdraw)
            if (filter.Type.HasValue)
            {
                query = query.Where(p => p.Type == filter.Type.Value);
            }

            // 3. Filter by Status
            if (filter.Status.HasValue)
            {
                query = query.Where(p => p.Status == filter.Status.Value);
            }

            // 4. Filter by Date Range
            if (filter.FromDate.HasValue)
            {
                query = query.Where(p => p.CreatedAt >= filter.FromDate.Value);
            }
            if (filter.ToDate.HasValue)
            {
                query = query.Where(p => p.CreatedAt <= filter.ToDate.Value);
            }

            // Count total before paging
            var totalCount = await query.CountAsync();

            // 5. Sorting
            if (filter.SortBy.Equals("Amount", StringComparison.OrdinalIgnoreCase))
            {
                query = filter.IsAscending ? query.OrderBy(p => p.Amount) : query.OrderByDescending(p => p.Amount);
            }
            else // Default by CreatedAt
            {
                query = filter.IsAscending ? query.OrderBy(p => p.CreatedAt) : query.OrderByDescending(p => p.CreatedAt);
            }

            // 6. Paging
            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<IEnumerable<Payment>> GetPendingWithdrawalsAsync()
        {
            // nhanh các đơn rút tiền đang chờ
            return await _context.Payments
                .Where(p => p.Type == PaymentType.Withdrawal && p.Status == PaymentStatus.Pending)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();
        }
    }
}
