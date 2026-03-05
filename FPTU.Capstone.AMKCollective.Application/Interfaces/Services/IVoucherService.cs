using FPTU.Capstone.AMKCollective.Application.DTOs.Voucher;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IVoucherService
    {
        // --- 1. Shop ---

        // Lấy danh sách voucher của Shop (có filter & paging)
        Task<PaginatedResult<VoucherResponse>> GetVouchersByShopAsync(Guid userId, VoucherFilterRequest filter);

        // Xem chi tiết voucher
        Task<VoucherResponse> GetVoucherByIdAsync(Guid id);

        // Tạo voucher
        Task<VoucherResponse> CreatePromotionalVoucherAsync(Guid userId, CreateVoucherRequest request);

        // Cập nhật voucher 
        Task<VoucherResponse> UpdateVoucherAsync(Guid userId, Guid voucherId, UpdateVoucherRequest request);

        // Xóa voucher (Chỉ xóa nếu chưa dùng, nếu dùng rồi sẽ báo lỗi)
        Task DeleteVoucherAsync(Guid userId, Guid voucherId);

        // Bật/Tắt voucher nhanh (Deactivate)
        Task ToggleVoucherStatusAsync(Guid userId, Guid voucherId);


        // --- 2. Customer ---

        // Lấy danh sách voucher của User (bao gồm voucher công khai và voucher riêng tặng mình)
        Task<List<VoucherResponse>> GetMyVouchersAsync(Guid userId);

        // Lấy voucher công khai của một Shop cụ thể (để hiển thị trên trang Shop Detail)
        Task<List<VoucherResponse>> GetShopPublicVouchersAsync(Guid shopId);

        Task<List<AppliedVoucherResponse>> GetAppliedVouchersByOrderIdAsync(Guid orderId);
        // --- 3. LOGIC NỘI BỘ / SYSTEM ---

        // Tạo voucher thương lượng (Auto-generated)
        Task<VoucherResponse> CreateNegotiationVoucherAsync(Guid userId, Guid targetUserId, decimal discountAmount, decimal minOrderValue);

        // Tạo voucher đền bù (Auto-generated)
        Task<VoucherResponse> CreateCompensationVoucherAsync(Guid userId, Guid targetUserId, decimal refundAmount);

        // Áp dụng voucher vào đơn hàng (hỗ trợ stacking — tối đa 2 voucher)
        Task<ApplyVoucherResult> ApplyVoucherAsync(Guid userId, Guid orderId, string code);

        // Gỡ một voucher cụ thể khỏi đơn hàng theo code
        Task<ApplyVoucherResult> RemoveSpecificVoucherAsync(Guid userId, Guid orderId, string voucherCode);

        // Gỡ toàn bộ voucher khỏi đơn hàng (dùng khi hủy đơn)
        Task RemoveAllVouchersAsync(Guid userId, Guid orderId);
        decimal CalculateVoucherDiscount(Voucher voucher, decimal baseAmount);
        Task<ApplicableVoucherResponse> GetApplicableVouchersAsync(Guid userId);

        Task<PaginatedResult<VoucherUsageResponse>> GetVoucherUsageHistoryAsync(Guid userId, Guid voucherId, int pageNumber, int pageSize);
        Task<PaginatedResult<VoucherUsageResponse>> GetAllVoucherUsagesAsync(Guid userId, int pageNumber, int pageSize);

    }
}
