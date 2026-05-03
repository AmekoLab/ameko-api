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
        Task AddPendingSalesToWalletAsync(Guid shopId, Guid orderId, decimal amount, decimal feeAmount = 0);
        Task ReleaseHeldMoneyAsync(Guid shopId, Guid orderId, decimal amount, decimal feeAmount = 0);
        Task RefundToWalletAsync(Guid userId, decimal amount, string reason, decimal penaltyAmount = 0m);
        Task DeductFundsForRefundAsync(Guid shopId, Guid orderId, decimal amount, bool isOrderCompleted);

        Task<PaginatedResult<WalletTransactionResponse>> GetTransactionsByFilterAsync(PaymentFilterRequest filter);
        Task<WalletTransactionDetailResponse> GetTransactionDetailAsync(Guid transactionId, Guid userId);

        Task ApproveWithdrawalAsync(Guid adminId, Guid paymentId, WithdrawalActionRequest request);
        Task RejectWithdrawalAsync(Guid adminId, Guid paymentId, WithdrawalActionRequest request);

        Task AdjustBalanceAsync(Guid adminId, AdjustBalanceRequest request);
        Task CreditPlatformFeeAsync(decimal amount, string description);
        Task<string> CreateDepositTransactionAsync(Guid userId, DepositRequest request);

        Task<WalletStatisticsResponse> GetWalletStatisticsAsync(Guid userId);
        Task<List<HeldTransactionResponse>> GetHeldTransactionsAsync(Guid userId);

        /// <summary>
        /// Báo cáo doanh thu / chi phí / rút tiền của shop theo tháng cho mục đích đối chiếu.
        /// </summary>
        Task<ShopStatementResponse> GetShopStatementAsync(Guid userId, int month, int year);
        //PIN
        Task<bool> IsPinCreatedAsync(Guid userId); // Kiểm tra xem user đã có PIN chưa
        Task SetupPinAsync(Guid userId, SetupWalletPinRequest request);
        Task ChangePinAsync(Guid userId, ChangeWalletPinRequest request);
        Task<bool> VerifyPinAsync(Guid userId, string pin); // Hàm dùng chung cho các feature sau này
        Task SendPinResetCodeAsync(Guid userId);
        Task ResetPinWithOtpAsync(Guid userId, ResetWalletPinRequest request);

        Task PayOrderGroupWithWalletAsync(Guid userId, Guid orderGroupId, decimal amount);

        // --- Withdrawal History ---
        /// <summary>Shop xem lịch sử rút tiền của mình (từ WithdrawalRequest table — có đầy đủ Status).</summary>
        Task<PaginatedResult<WithdrawalSummaryResponse>> GetMyWithdrawalHistoryAsync(Guid userId, int pageIndex, int pageSize);

        // --- Admin ---
        /// <summary>Admin xem danh sách đơn rút tiền đang Pending (fix cho endpoint bị broken).</summary>
        Task<PaginatedResult<WithdrawalSummaryResponse>> GetAdminPendingWithdrawalsAsync(int pageIndex, int pageSize, string? shopName = null);
        Task<WalletTransactionDetailResponse> GetTransactionDetailForAdminAsync(Guid transactionId);

        /// <summary>Admin xem lịch sử đơn rút đã xử lý (Completed / Rejected).</summary>
        Task<PaginatedResult<WithdrawalSummaryResponse>> GetAdminProcessedWithdrawalsAsync(int pageIndex, int pageSize);

        /// <summary>Admin xem thông tin ví của một user/shop cụ thể.</summary>
        Task<WalletResponse?> GetWalletByUserIdForAdminAsync(Guid targetUserId);
    }
}

