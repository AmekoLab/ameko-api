using FPTU.Capstone.AMKCollective.Application.DTOs.Voucher;
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


        // --- 3. LOGIC NỘI BỘ / SYSTEM ---

        // Tạo voucher thương lượng (Auto-generated)
        Task<VoucherResponse> CreateNegotiationVoucherAsync(Guid userId, Guid targetUserId, decimal discountAmount, decimal minOrderValue);

        // Tạo voucher đền bù (Auto-generated)
        Task<VoucherResponse> CreateCompensationVoucherAsync(Guid userId, Guid targetUserId, decimal refundAmount);

        // Áp dụng voucher vào đơn hàng
        Task<decimal> ApplyVoucherAsync(Guid userId, Guid orderId, string code);

        // Gỡ voucher khỏi đơn hàng
        Task RemoveVoucherAsync(Guid userId, Guid orderId);
    }
}
