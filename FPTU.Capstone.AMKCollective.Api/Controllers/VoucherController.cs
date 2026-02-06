using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs.Voucher;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class VoucherController : BaseApiController
    {
        private readonly IVoucherService _voucherService;

        public VoucherController(IVoucherService voucherService)
        {
            _voucherService = voucherService;
        }

        /// <summary>
        /// Tạo Voucher Khuyến mãi (Promotion) - Dành cho Shop/Admin
        /// </summary>
        [HttpPost("promotion")]
        public async Task<IActionResult> CreatePromotionalVoucher([FromBody] CreateVoucherRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return ErrorResponse<object>("Validation failed", errors);
            }

            try
            {
                var userId = GetCurrentUserId();
                // Lưu ý: Logic check Role (Shop/Admin) nên nằm ở Service hoặc Attribute [Authorize(Roles="Shop,Admin")]

                var result = await _voucherService.CreatePromotionalVoucherAsync(userId, request);
                return SuccessResponse(result, "Promotional voucher created successfully");
            }
            catch (Exception ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Tạo Voucher Thương lượng (Negotiation) - Dành cho Shop chốt deal với khách
        /// </summary>
        [HttpPost("negotiation")]
        public async Task<IActionResult> CreateNegotiationVoucher([FromBody] CreateNegotiationRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return ErrorResponse<object>("Validation failed", errors);
            }

            try
            {
                var shopId = GetCurrentUserId();
                // Check Role Shop ở đây hoặc dùng Policy

                var result = await _voucherService.CreateNegotiationVoucherAsync(
                    shopId,
                    request.TargetUserId,
                    request.DiscountAmount,
                    request.MinOrderValue
                );

                return SuccessResponse(result, "Negotiation voucher created successfully");
            }
            catch (Exception ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Áp dụng Voucher vào đơn hàng
        /// </summary>
        [HttpPost("apply")]
        public async Task<IActionResult> ApplyVoucher([FromBody] ApplyVoucherRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var discountAmount = await _voucherService.ApplyVoucherAsync(userId, request.OrderId, request.Code);

                // Trả về số tiền được giảm để FE hiển thị
                return SuccessResponse(new { DiscountAmount = discountAmount }, "Voucher applied successfully");
            }
            catch (Exception ex)
            {
                // Trả về lỗi 400 kèm message chi tiết (VD: Hết hạn, chưa đủ tiền...)
                return ErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Gỡ bỏ Voucher khỏi đơn hàng
        /// </summary>
        [HttpPost("remove/{orderId}")]
        public async Task<IActionResult> RemoveVoucher(Guid orderId)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _voucherService.RemoveVoucherAsync(userId, orderId);

                return SuccessResponse("Voucher removed successfully");
            }
            catch (Exception ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Lấy danh sách Voucher khả dụng của tôi (Voucher công khai + Voucher riêng)
        /// </summary>
        [HttpGet("my-vouchers")]
        public async Task<IActionResult> GetMyVouchers()
        {
            try
            {
                var userId = GetCurrentUserId();
                var vouchers = await _voucherService.GetMyVouchersAsync(userId);

                return SuccessResponse(vouchers, "Get my vouchers successfully");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        // Helper private để lấy User Id từ Token
        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out Guid userId))
            {
                return userId;
            }
            throw new UnauthorizedAccessException("User ID not found in token");
        }
    }
}
    
