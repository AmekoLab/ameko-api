using FPTU.Capstone.AMKCollective.Application.DTOs.Payment;
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
    public class TransactionRepository : ITransactionRepository
    {
        private readonly ApplicationDbContext _context;

        public TransactionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Transaction transaction)
        {
            await _context.Transactions.AddAsync(transaction);
        }

        public async Task<List<Transaction>> GetByWalletIdAsync(Guid walletId)
        {
            return await _context.Transactions
                .Include(t => t.RelatedOrder)
                .Where(t => t.WalletId == walletId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }
        public async Task<(List<Transaction> Items, int TotalCount)> GetTransactionsByFilterAsync(PaymentFilterRequest filter)
        {
            var query = _context.Transactions
                .Include(t => t.RelatedOrder)
                .AsQueryable();

            if (filter.UserId.HasValue)
            {
                query = query.Where(t => t.Wallet != null && t.Wallet.UserId == filter.UserId.Value);
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(t => t.CreatedAt >= filter.FromDate.Value);
            }

            if (filter.ToDate.HasValue)
            {
                query = query.Where(t => t.CreatedAt <= filter.ToDate.Value);
            }

            int totalCount = await query.CountAsync();

            if (filter.SortBy.Equals("CreatedAt", StringComparison.OrdinalIgnoreCase))
            {
                query = filter.IsAscending ? query.OrderBy(t => t.CreatedAt) : query.OrderByDescending(t => t.CreatedAt);
            }

            var items = await query
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return (items, totalCount);
        }
    }
}
    
