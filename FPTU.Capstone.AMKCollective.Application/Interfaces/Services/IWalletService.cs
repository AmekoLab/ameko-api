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
        Task DeductFundsForRefundAsync(Guid shopId, Guid orderId, decimal amount, bool isOrderCompleted);

        Task<PaginatedResult<WalletTransactionResponse>> GetTransactionsByFilterAsync(PaymentFilterRequest filter);
        Task ApproveWithdrawalAsync(Guid adminId, Guid paymentId, WithdrawalActionRequest request);
        Task RejectWithdrawalAsync(Guid adminId, Guid paymentId, WithdrawalActionRequest request);

        Task AdjustBalanceAsync(Guid adminId, AdjustBalanceRequest request);
        Task<string> CreateDepositTransactionAsync(Guid userId, DepositRequest request);

        Task<WalletStatisticsResponse> GetWalletStatisticsAsync(Guid userId);
        Task<List<HeldTransactionResponse>> GetHeldTransactionsAsync(Guid userId);
        //PIN
        Task<bool> IsPinCreatedAsync(Guid userId); // Kiểm tra xem user đã có PIN chưa
        Task SetupPinAsync(Guid userId, SetupWalletPinRequest request);
        Task ChangePinAsync(Guid userId, ChangeWalletPinRequest request);
        Task<bool> VerifyPinAsync(Guid userId, string pin); // Hàm dùng chung cho các feature sau này
        Task SendPinResetCodeAsync(Guid userId);
        Task ResetPinWithOtpAsync(Guid userId, ResetWalletPinRequest request);
    }
}

