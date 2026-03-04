using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IOrderVoucherRepository
    {
        /// <summary>Get all OrderVouchers for an order, ordered by application order.</summary>
        Task<IEnumerable<OrderVoucher>> GetByOrderIdAsync(Guid orderId);

        /// <summary>Get a specific OrderVoucher by OrderId and VoucherId.</summary>
        Task<OrderVoucher?> GetByOrderAndVoucherAsync(Guid orderId, Guid voucherId);

        /// <summary>Update voucher</summary>
        void Update(OrderVoucher orderVoucher);
        /// <summary>Add a new OrderVoucher.</summary>
        Task AddAsync(OrderVoucher orderVoucher);

        /// <summary>Delete an OrderVoucher.</summary>
        void Delete(OrderVoucher orderVoucher);

        /// <summary>Delete all OrderVouchers for an order.</summary>
        Task DeleteAllByOrderIdAsync(Guid orderId);

        Task<(IEnumerable<OrderVoucher> Items, int TotalCount)> GetUsageByVoucherIdAsync(Guid voucherId, int pageNumber, int pageSize);

        Task<(IEnumerable<OrderVoucher> Items, int TotalCount)> GetAllUsagesAsync(Guid? creatorId, int pageNumber, int pageSize);
    }
}
