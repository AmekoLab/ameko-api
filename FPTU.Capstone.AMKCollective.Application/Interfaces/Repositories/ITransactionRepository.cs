using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface ITransactionRepository
    {
        Task<Transaction?> GetByIdAsync(Guid id);
        Task AddAsync(Transaction transaction);
        Task<List<Transaction>> GetByWalletIdAsync(Guid walletId);
        Task<(List<Transaction> Items, int TotalCount)> GetTransactionsByFilterAsync(PaymentFilterRequest filter);

        /// <summary>
        /// Kiểm tra xem transaction với orderId và type đã tồn tại chưa (dùng cho idempotency check).
        /// Query trực tiếp DB, không load toàn bộ dữ liệu.
        /// </summary>
        Task<bool> ExistsByOrderAndTypeAsync(Guid walletId, Guid orderId, TransactionType type);
        Task<decimal> SumAmountByTypeAsync(Guid walletId, TransactionType type, int? month = null, int? year = null);
        Task<List<Transaction>> GetByWalletIdAndTypeAsync(Guid walletId, TransactionType type);

        /// <summary>
        /// Lấy toàn bộ transaction trong khoảng [from, to) — sort asc theo CreatedAt.
        /// Dùng cho báo cáo statement theo kỳ.
        /// </summary>
        Task<List<Transaction>> GetByWalletIdInRangeAsync(Guid walletId, DateTime fromUtc, DateTime toUtc);

        /// <summary>
        /// Lấy transaction gần nhất trước thời điểm <paramref name="beforeUtc"/> để tính số dư đầu kỳ.
        /// Trả về null nếu không có giao dịch nào trước đó.
        /// </summary>
        Task<Transaction?> GetLastBeforeAsync(Guid walletId, DateTime beforeUtc);

        /// <summary>
        /// Tổng FeeAmount theo type trong khoảng thời gian — dùng để tổng hợp phí platform của shop.
        /// </summary>
        Task<decimal> SumFeeByTypeInRangeAsync(Guid walletId, TransactionType type, DateTime fromUtc, DateTime toUtc);
    }
}
