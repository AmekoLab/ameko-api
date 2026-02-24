using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
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
        /// Create a Promotional Voucher - For Shop/Admin.
        /// Promotional vouchers are general discount vouchers created by shops or admins.
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
                // Note: Role check logic (Shop/Admin) should be placed in Service or use [Authorize(Roles="Shop,Admin")] attribute

                var result = await _voucherService.CreatePromotionalVoucherAsync(userId, request);
                return SuccessResponse(result, "Promotional voucher created successfully");
            }
            catch (Exception ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Create a Negotiation Voucher - For Shop to finalize deals with customers.
        /// Negotiation vouchers are special discount offers created by shops for specific customers.
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
                // Check Shop role here or use authorization policy

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
        /// Apply Voucher to Order (Supports Stacking - Maximum 2 vouchers per order).
        /// Business Rules:
        ///   Promotion + Compensation - Allowed
        ///   Negotiation + Compensation - Allowed
        ///   Promotion + Promotion - Not Allowed
        ///   Promotion + Negotiation - Not Allowed
        ///   Compensation + Compensation - Not Allowed
        /// </summary>
        [HttpPost("apply")]
        public async Task<IActionResult> ApplyVoucher([FromBody] ApplyVoucherRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _voucherService.ApplyVoucherAsync(userId, request.OrderId, request.Code);
                return SuccessResponse(result, "Voucher applied successfully");
            }
            catch (Exception ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Remove a Specific Voucher from Order by Voucher Code.
        /// Returns updated stacking information and remaining discount totals.
        /// </summary>
        [HttpDelete("remove/{orderId}/{voucherCode}")]
        public async Task<IActionResult> RemoveSpecificVoucher(Guid orderId, string voucherCode)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _voucherService.RemoveSpecificVoucherAsync(userId, orderId, voucherCode);
                return SuccessResponse(result, "Voucher removed successfully");
            }
            catch (Exception ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Remove All Vouchers from Order.
        /// Used when cancelling order or resetting shopping cart. Clears all applied discounts.
        /// </summary>
        [HttpDelete("remove-all/{orderId}")]
        public async Task<IActionResult> RemoveAllVouchers(Guid orderId)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _voucherService.RemoveAllVouchersAsync(userId, orderId);
                return SuccessResponse("All vouchers removed from order");
            }
            catch (Exception ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Get List of Available Vouchers for Current User.
        /// Returns both public vouchers and privately assigned vouchers for the user.
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

        /// <summary>
        /// Gets a list of applicable vouchers based on the user's current shopping cart.
        /// Automatically categorizes vouchers into System/Platform Vouchers (applied to the whole cart) 
        /// and Shop Vouchers (applied only to specific shop items).
        /// Filters out vouchers that do not meet the Minimum Order Value (MinOrderValue).
        /// </summary>
        /// <remarks>
        /// Frontend Usage:
        /// - Use `systemVouchers` for the bottom-level cart discount section.
        /// - Iterate through `shopVoucherGroups` and match `shopId` to display vouchers under each specific shop's item list.
        /// </remarks>
        /// <response code="200">Returns the structured list of applicable vouchers.</response>
        /// <response code="401">If the user is not authenticated.</response>
        [HttpGet("applicable")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<ApplicableVoucherResponse>), 200)]
        public async Task<IActionResult> GetApplicableVouchersForCart()
        {
            try
            {
                var userId = GetCurrentUserId();

                var result = await _voucherService.GetApplicableVouchersAsync(userId);

                return SuccessResponse(result, "Applicable vouchers retrieved successfully.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<ApplicableVoucherResponse>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<ApplicableVoucherResponse>(ex.Message);
            }
        }


        // Helper method to extract User Id from Token
        //private Guid GetCurrentUserId()
        //{
        //    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        //    if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out Guid userId))
        //    {
        //        return userId;
        //    }
        //    throw new UnauthorizedAccessException("User ID not found in token");
        //}
    }
}
    
