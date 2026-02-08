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
        Task<VoucherResponse> CreatePromotionalVoucherAsync(Guid userId, CreateVoucherRequest request);
        Task<VoucherResponse> CreateNegotiationVoucherAsync(Guid shopId, Guid targetUserId, decimal discountAmount, decimal minOrderValue);
        Task<VoucherResponse> CreateCompensationVoucherAsync(Guid shopId, Guid targetUserId, decimal refundAmount);

        Task<decimal> ApplyVoucherAsync(Guid userId, Guid orderId, string code);
        Task RemoveVoucherAsync(Guid userId, Guid orderId);

        Task<List<VoucherResponse>> GetMyVouchersAsync(Guid userId);
    }
}
