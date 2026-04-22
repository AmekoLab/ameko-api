using FPTU.Capstone.AMKCollective.Application.DTOs.AdminDashboard;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IPaymentRepository
    {
        Task AddAsync(Payment payment, CancellationToken token = default);
        Task<Payment?> GetByStripeSessionIdAsync(string sessionId, CancellationToken token = default);
        Task<Payment?> GetByStripePaymentIntentIdAsync(string paymentIntentId,  CancellationToken token = default);
        Task<IEnumerable<Payment>> GetByOrderGroupIdAsync(Guid orderGroupId, CancellationToken token = default);

        Task<int> SaveChangesAsync(CancellationToken token = default);
        void Update(Payment payment);

        Task<Payment?> GetPaymentByOrderGroupIdAsync(Guid orderGroupId);
        Task<IEnumerable<Payment>> GetByUserIdAsync(Guid userId, CancellationToken token = default);
        //Task<(decimal TotalRevenue, decimal TotalWithdrawn, decimal PendingWithdrawal, decimal ThisMonthRevenue)> GetPaymentStatsByWalletIdAsync(Guid walletId);
        //Task<List<Payment>> GetHeldPaymentsByWalletIdAsync(Guid walletId);

        // 1. Lấy Payment theo ID 
        Task<Payment?> GetByIdAsync(Guid id);

        Task<Payment?> GetPaymentBySessionIdAsync(string sessionId);

        // 2. Lấy danh sách Payment có lọc và phân trang (Dùng cho Admin Dashboard & User History)
        Task<(IEnumerable<Payment> Items, int TotalCount)> GetPaymentsByFilterAsync(PaymentFilterRequest filter);

        // 3. Lấy danh sách chờ duyệt nhanh cho Admin
        //Task<IEnumerable<Payment>> GetPendingWithdrawalsAsync();
        /// <summary>
        /// Returns all payments in the provided time range for dashboard analytics.
        /// </summary>
        Task<List<Payment>> GetPaymentsForDashboardAsync(DateTime fromUtc, DateTime toUtc, CancellationToken token = default);

        /// <summary>
        /// Returns pre-aggregated payment stats computed entirely in the database.
        /// </summary>
        Task<PaymentDashboardStats> GetPaymentStatsForDashboardAsync(DateTime fromUtc, DateTime toUtc, CancellationToken token = default);

    }
}
