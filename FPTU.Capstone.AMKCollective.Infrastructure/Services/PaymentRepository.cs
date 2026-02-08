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
    }
}
