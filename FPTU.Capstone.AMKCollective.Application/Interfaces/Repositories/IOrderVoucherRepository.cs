using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IOrderVoucherRepository
    {
        /// <summary>Lấy tất cả OrderVoucher của một đơn hàng, sắp xếp theo thứ tự áp dụng.</summary>
        Task<IEnumerable<OrderVoucher>> GetByOrderIdAsync(Guid orderId);

        /// <summary>Lấy OrderVoucher cụ thể theo OrderId + VoucherId.</summary>
        Task<OrderVoucher?> GetByOrderAndVoucherAsync(Guid orderId, Guid voucherId);

        /// <summary>Thêm một OrderVoucher mới.</summary>
        Task AddAsync(OrderVoucher orderVoucher);

        /// <summary>Xóa một OrderVoucher.</summary>
        void Delete(OrderVoucher orderVoucher);

        /// <summary>Xóa toàn bộ OrderVoucher của một đơn hàng.</summary>
        Task DeleteAllByOrderIdAsync(Guid orderId);
    }
}
