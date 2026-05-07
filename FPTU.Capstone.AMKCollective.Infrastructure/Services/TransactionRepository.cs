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
    public class TransactionRepository : ITransactionRepository
    {
        private readonly ApplicationDbContext _context;

        public TransactionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Transaction?> GetByIdAsync(Guid id)
        {
            return await _context.Transactions
                .Include(t => t.Wallet)
                    .ThenInclude(w => w.User)
                        .ThenInclude(u => u.ShopProfile)
                .FirstOrDefaultAsync(t => t.Id == id);
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
                .Include(t => t.Wallet)
                .ThenInclude(w => w.User)
                .ThenInclude(u => u!.ShopProfile)
                .AsQueryable();

            if (filter.UserId.HasValue)
            {
                query = query.Where(t => t.Wallet != null && t.Wallet.UserId == filter.UserId.Value);
            }

            if (filter.Type.HasValue)
            {
                TransactionType? transactionType = filter.Type.Value switch
                {
                    PaymentType.Withdrawal => TransactionType.Withdrawal,
                    PaymentType.Deposit => TransactionType.Deposit,
                    PaymentType.OrderPayment => TransactionType.OrderPayment,
                    PaymentType.PaymentByWallet => TransactionType.OrderPayment,
                    PaymentType.SalesPending => TransactionType.SalesPending,
                    PaymentType.SalesReleased => TransactionType.SalesRevenue,
                    PaymentType.ManualAdjustment => TransactionType.ManualAdjustment,
                    PaymentType.RefundToWallet => TransactionType.OrderRefund,
                    _ => null
                };

                if (transactionType.HasValue)
                {
                    query = query.Where(t => t.Type == transactionType.Value);
                }
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(t => t.CreatedAt >= filter.FromDate.Value);
            }

            if (filter.ToDate.HasValue)
            {
                query = query.Where(t => t.CreatedAt <= filter.ToDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.ShopName))
            {
                var pattern = $"%{filter.ShopName.Trim()}%";
                query = query.Where(t =>
                    t.Wallet != null &&
                    t.Wallet.User != null &&
                    t.Wallet.User.ShopProfile != null &&
                    EF.Functions.Like(t.Wallet.User.ShopProfile.ShopName, pattern));
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

        public async Task<bool> ExistsByOrderAndTypeAsync(Guid walletId, Guid orderId, TransactionType type)
        {
            return await _context.Transactions.AnyAsync(t =>
                t.WalletId == walletId &&
                t.RelatedOrderId == orderId &&
                t.Type == type);
        }
        public async Task<decimal> SumAmountByTypeAsync(Guid walletId, TransactionType type, int? month = null, int? year = null)
        {
            var query = _context.Transactions
                .Where(t => t.WalletId == walletId && t.Type == type);

            if (month.HasValue && year.HasValue)
                query = query.Where(t => t.CreatedAt.Month == month.Value && t.CreatedAt.Year == year.Value);

            return await query.SumAsync(t => (decimal?)t.Amount) ?? 0m;
        }

        public async Task<List<Transaction>> GetByWalletIdAndTypeAsync(Guid walletId, TransactionType type)
        {
            return await _context.Transactions
                .Include(t => t.RelatedOrder)
                .Where(t => t.WalletId == walletId && t.Type == type)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Transaction>> GetByWalletIdInRangeAsync(Guid walletId, DateTime fromUtc, DateTime toUtc)
        {
            return await _context.Transactions
                .Include(t => t.RelatedOrder)
                .Where(t => t.WalletId == walletId && t.CreatedAt >= fromUtc && t.CreatedAt < toUtc)
                .OrderBy(t => t.CreatedAt)   // ASC để tính closingBalance — newest = last
                .ToListAsync();
        }

        public async Task<List<Transaction>> GetByWalletIdInRangeDescAsync(Guid walletId, DateTime fromUtc, DateTime toUtc)
        {
            return await _context.Transactions
                .Include(t => t.RelatedOrder)
                .Where(t => t.WalletId == walletId && t.CreatedAt >= fromUtc && t.CreatedAt < toUtc)
                .OrderByDescending(t => t.CreatedAt)  // DESC cho FE hiển thị mới nhất trước
                .ToListAsync();
        }

        public async Task<Transaction?> GetLastBeforeAsync(Guid walletId, DateTime beforeUtc)
        {
            return await _context.Transactions
                .Where(t => t.WalletId == walletId && t.CreatedAt < beforeUtc)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<decimal> SumFeeByTypeInRangeAsync(Guid walletId, TransactionType type, DateTime fromUtc, DateTime toUtc)
        {
            return await _context.Transactions
                .Where(t => t.WalletId == walletId
                            && t.Type == type
                            && t.CreatedAt >= fromUtc
                            && t.CreatedAt < toUtc)
                .SumAsync(t => (decimal?)t.FeeAmount) ?? 0m;
        }
    }
}
    
