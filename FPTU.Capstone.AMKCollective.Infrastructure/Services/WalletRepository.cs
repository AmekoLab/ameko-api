using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class WalletRepository : IWalletRepository
    {
        private readonly ApplicationDbContext _context;

        public WalletRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Wallet?> GetByUserIdAsync(Guid userId)
        {
            return await _context.Wallets
                .FirstOrDefaultAsync(w => w.UserId == userId && !w.IsDeleted);
        }

        public async Task<Wallet?> GetByIdAsync(Guid id)
        {
            return await _context.Wallets
                .FirstOrDefaultAsync(w => w.Id == id && !w.IsDeleted);
        }

        public async Task AddAsync(Wallet wallet)
        {
            await _context.Wallets.AddAsync(wallet);
        }

        public void Update(Wallet wallet)
        {
            _context.Wallets.Update(wallet);
        }

        public void Delete(Wallet wallet)
        {
            wallet.IsDeleted = true;
            _context.Wallets.Update(wallet);
        }
        public async Task<(bool Success, decimal OldBalance, decimal OldHeldBalance)> UpdateBalancesAsync(Guid walletId, decimal balanceChange, decimal heldBalanceChange = 0, bool allowNegative = false)
        {
            // [FIX #6] Đọc snapshot trước khi update — dùng AsNoTracking để tránh conflict với EF change tracker
            var snapshot = await _context.Wallets
                .AsNoTracking()
                .Where(w => w.Id == walletId)
                .Select(w => new { w.Balance, w.HeldBalance })
                .FirstOrDefaultAsync();

            if (snapshot == null)
                return (false, 0, 0);

            var query = _context.Wallets.Where(w => w.Id == walletId);

            if (!allowNegative)
            {
                query = query.Where(w => w.Balance + balanceChange >= 0 && w.HeldBalance + heldBalanceChange >= 0);
            }
            int rowsAffected = await query.ExecuteUpdateAsync(s => s
                .SetProperty(w => w.Balance, w => w.Balance + balanceChange)
                .SetProperty(w => w.HeldBalance, w => w.HeldBalance + heldBalanceChange));

            return (rowsAffected > 0, snapshot.Balance, snapshot.HeldBalance);
        }
    }
}
