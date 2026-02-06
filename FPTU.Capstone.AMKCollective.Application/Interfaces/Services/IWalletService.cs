using FPTU.Capstone.AMKCollective.Application.DTOs.Wallet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IWalletService
    {
        Task<WalletResponse> GetWalletByUserIdAsync(Guid userId);
        Task<List<WalletTransactionResponse>> GetTransactionsAsync(Guid userId);
        Task CreateWalletAsync(Guid userId);
        Task RequestWithdrawalAsync(Guid userId, WithdrawRequest request);
        Task PayOrderWithWalletAsync(Guid userId, Guid orderId, decimal amount);
        Task AddPendingSalesToWalletAsync(Guid shopId, Guid orderId, decimal amount);
        Task ReleaseHeldMoneyAsync(Guid shopId, Guid orderId, decimal amount);
        Task RefundToWalletAsync(Guid userId, decimal amount, string reason);
    }
}
