using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IWalletRepository
    {
        Task<Wallet?> GetByUserIdAsync(Guid userId);
        Task<Wallet?> GetByIdAsync(Guid id);
        Task AddAsync(Wallet wallet);
        void Update(Wallet wallet);
        void Delete(Wallet wallet);
        Task<bool> UpdateBalancesAsync(Guid walletId, decimal balanceChange, decimal heldBalanceChange = 0, bool allowNegative = false);
    }
}
