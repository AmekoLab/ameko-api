using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs.Voucher;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
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

        /// <summary>
        /// Get Shop's Vouchers (Filter & Pagination).
        /// Role: Shop Owner.
        /// Description: Retrieves a paginated list of vouchers created by the current shop with optional filters (status, date, code).
        /// </summary>
        [HttpGet("shop")]
        [SwaggerOperation(Summary = "Get Shop's Vouchers with Filter")]
        public async Task<IActionResult> GetVouchersByShop([FromQuery] VoucherFilterRequest filter)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _voucherService.GetVouchersByShopAsync(userId, filter);
                return SuccessResponse(result, "Get shop vouchers successfully");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Get Voucher Details.
        /// Role: Shop Owner.
        /// Description: Retrieves detailed information of a specific voucher by ID.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetVoucherById(Guid id)
        {
            try
            {
                var result = await _voucherService.GetVoucherByIdAsync(id);
                return SuccessResponse(result, "Get voucher details successfully");
            }
            catch (KeyNotFoundException ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Update Voucher.
        /// Role: Shop Owner.
        /// Description: Updates allowed fields (Name, Description, EndDate, UsageLimit, Status) of an existing voucher.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateVoucher(Guid id, [FromBody] UpdateVoucherRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _voucherService.UpdateVoucherAsync(userId, id, request);
                return SuccessResponse(result, "Voucher updated successfully");
            }
            catch (UnauthorizedAccessException ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Delete Voucher.
        /// Role: Shop Owner.
        /// Description: Soft deletes a voucher. This is only allowed if the voucher has NOT been used by any customer.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteVoucher(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _voucherService.DeleteVoucherAsync(userId, id);
                return SuccessResponse("Voucher deleted successfully");
            }
            catch (InvalidOperationException ex)
            {
                // Returns 400 if voucher is already used
                return ErrorResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Toggle Voucher Status.
        /// Role: Shop Owner.
        /// Description: Quickly switches the voucher status between 'Active' and 'Disabled' (Deactivate).
        /// </summary>
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> ToggleVoucherStatus(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _voucherService.ToggleVoucherStatusAsync(userId, id);
                return SuccessResponse("Voucher status updated successfully");
            }
            catch (Exception ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Get Shop's Public Vouchers.
        /// Role: Public / Guest / Customer.
        /// Description: Retrieves all active, public vouchers for a specific shop. Useful for Shop Detail page.
        /// </summary>
        [HttpGet("shop/{shopId}/public")]
        [AllowAnonymous] // Allow guests to see shop's vouchers
        public async Task<IActionResult> GetShopPublicVouchers(Guid shopId)
        {
            try
            {
                var vouchers = await _voucherService.GetShopPublicVouchersAsync(shopId);
                return SuccessResponse(vouchers, "Get shop public vouchers successfully");
            }
            catch (Exception ex)
            {
                return ErrorResponse<object>(ex.Message);
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
    
