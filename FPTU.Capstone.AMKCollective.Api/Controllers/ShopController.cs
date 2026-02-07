using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using FPTU.Capstone.AMKCollective.Application.DTOs.Shop;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    [Route("api/v1/shops")]
    [ApiController]
    public class ShopController : BaseApiController
    {
        private readonly IShopService _shopService;
        private readonly ILogger<ShopController> _logger;

        public ShopController(IShopService shopService, ILogger<ShopController> logger)
        {
            _shopService = shopService;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        [SwaggerOperation(
    Summary = "List Marketplace Shops",
    Description = "Retrieves a paginated list of active shops in the marketplace."
)]
        [SwaggerResponse(200, "Shops retrieved successfully", typeof(ApiResponse<object>))]
        public async Task<IActionResult> GetMarketplaceShops([FromQuery] string? searchTerm, [FromQuery] int page = 1, [FromQuery] int size = 10)
        {
            try
            {
                var (items, total) = await _shopService.GetMarketplaceShopAsync(searchTerm, page, size);
                var response = new
                {
                    items,
                    pagination = new { page, size, totalCount = total }
                };
                return SuccessResponse(response);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting marketplace shops");
                return ServerErrorResponse<string>("Error getting shops");
            }
        }

        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        [SwaggerOperation(
    Summary = "Get Shop Public Profile",
    Description = "Retrieves public details of a specific shop by ID."
)]
        [SwaggerResponse(200, "Shop profile retrieved")]
        [SwaggerResponse(404, "Shop not found")]
        public async Task<IActionResult> GetShopPublicProfile(Guid id)
        {
            try
            {
                var shop = await _shopService.GetShopPublicProfileAsync(id);
                return SuccessResponse(shop);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting shop public profile {Id}", id);
                return ServerErrorResponse<string>("Error getting shop profile");
            }
        }

        [HttpGet("my-shop")]
        //[Authorize]
        [SwaggerOperation(
    Summary = "Get My Shop",
    Description = "Retrieves the shop profile associated with the current authenticated user."
)]
        [SwaggerResponse(200, "Shop profile retrieved")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(404, "Shop not found for this user")]
        public async Task<IActionResult> GetMyShop()
        {
            try
            {
                var userId = GetCurrentUserId();
                var shop = await _shopService.GetMyShopAsync(userId);
                return SuccessResponse(shop);
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse<string>("Your shop not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting my shop");
                return ServerErrorResponse<string>("Error getting shop");
            }
        }

        [HttpPost("register")]
        //[Authorize]
        [SwaggerOperation(
    Summary = "Register New Shop",
    Description = "Submit a request to upgrade the current user account to a Shop account."
)]
        [SwaggerResponse(200, "Registration submitted successfully")]
        [SwaggerResponse(400, "Validation error")]
        [SwaggerResponse(401, "Unauthorized")]
        public async Task<IActionResult> RegisterShop([FromForm] CreateShopRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _shopService.RegisterShopAsync(userId, request);
                return SuccessResponse(result, "Submit successfully, waiting for approve.");
            }
            catch (InvalidOperationException ex)
            {
                return ErrorResponse<string>(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return ErrorResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering shop");
                return ServerErrorResponse<string>("System error during shop registration ");
            }
        }

        [HttpPut("profile")]
        //[Authorize]
        [SwaggerOperation(
    Summary = "Update Shop Profile",
    Description = "Updates the details of the authenticated user's shop."
)]
        [SwaggerResponse(200, "Shop updated successfully")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(404, "Shop not found")]
        public async Task<IActionResult> UpdateMyShop([FromForm] UpdateShopRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _shopService.UpdateMyShopAsync(userId, request);
                return SuccessResponse("Update shop profile successfully");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating shop profile");
                return ServerErrorResponse<string>("Error during update shop profile");
            }
        }

        [HttpGet("admin/list")]
        // [Authorize(Roles ="Admin")]
        [SwaggerOperation(
    Summary = "Admin: List All Shops",
    Description = "Retrieves a list of all shops with status filtering for administrative purposes."
)]
        [SwaggerResponse(200, "List retrieved successfully")]
        [SwaggerResponse(403, "Forbidden - Requires Admin role")]
        public async Task<IActionResult> GetShopForAdmin([FromQuery] string? searchTerm,
          [FromQuery] ShopStatus? status,
          [FromQuery] int page = 1,
          [FromQuery] int size = 20)
        {
            try
            {
                var (items, total) = await _shopService.GetShopForAdminAsync(searchTerm, status, page, size);
                var response = new
                {
                    items,
                    pagination = new { page, size, totalCount = total }
                };
                return SuccessResponse(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting admin shop list");
                return ServerErrorResponse<string>("system error");
            }
        }

        [HttpPost("admin/{id:guid}/approve")]
        //[Authorize(Roles="Admin")]
        [SwaggerOperation(
    Summary = "Admin: Approve/Reject Shop",
    Description = "Approve or reject a shop registration request."
)]
        [SwaggerResponse(200, "Status updated successfully")]
        [SwaggerResponse(403, "Forbidden - Requires Admin role")]
        [SwaggerResponse(404, "Shop not found")]
        public async Task<IActionResult> ApproveShop(Guid id, [FromBody] ApproveShopRequest request)
        {
            try
            {
                await _shopService.ApproveShopAsync(id, request);
                return SuccessResponse($"Updated shop status: {request.Status}"); ;
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<string>(ex.Message);
                return NotFoundResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving shop {Id}", id);
                return ServerErrorResponse<string>("Error during approve shop");
            }
        }

        /// <summary>
        /// Admin: Get list of shops pending approval
        /// </summary>
        /// <param name="page">Page number (starting from 1)</param>
        /// <param name="size">Records per page</param>
        /// <returns>Paginated list of shops pending approval</returns>
        [HttpGet("admin/pending-approval")]
        //[Authorize(Roles="Admin")]
        [SwaggerOperation(
            Summary = "Admin: Get Pending Approval Shops",
            Description = "Retrieves a paginated list of shops awaiting approval."
        )]
        [SwaggerResponse(200, "List retrieved successfully", typeof(ApiResponse<object>))]
        [SwaggerResponse(403, "Forbidden - Requires Admin role")]
        public async Task<IActionResult> GetPendingApprovalShops([FromQuery] int page = 1, [FromQuery] int size = 20)
        {
            try
            {
                var (items, total) = await _shopService.GetAllPendingApprovalShopAsync(page, size);
                var response = new
                {
                    items,
                    pagination = new { page, size, totalCount = total }
                };
                return SuccessResponse(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending approval shops");
                return ServerErrorResponse<string>("Error retrieving pending shops");
            }
        }

        /// <summary>
        /// Shop Owner: Deactivate shop
        /// </summary>
        /// <param name="id">ID of the shop to deactivate</param>
        /// <returns>Deactivation success message</returns>
        [HttpPut("deactivate")]
        [Authorize(Roles = "Shop")]
        [SwaggerOperation(
            Summary = "Shop Owner: Deactivate Shop",
            Description = "Shop owner deactivates their own shop."
        )]
        [SwaggerResponse(200, "Shop deactivated successfully")]
        [SwaggerResponse(403, "Forbidden - Requires Shop role")]
        [SwaggerResponse(404, "Shop not found")]
        [SwaggerResponse(400, "Only active shops can be deactivated")]
        public async Task<IActionResult> DeactivateShop()
        {
            try
            {
                var userId = GetCurrentUserId();
                await _shopService.DeactivateShopAsync(userId);
                return SuccessResponse("Shop deactivated successfully");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<string>(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ErrorResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating shop");
                return ServerErrorResponse<string>("Error during shop deactivation");
            }
        }

        /// <summary>
        /// Admin: Deactivate shop (not ban)
        /// </summary>
        /// <param name="id">ID of the shop to deactivate</param>
        /// <returns>Deactivation success message</returns>
        [HttpPut("admin/{id:guid}/deactivate")]
        [Authorize(Roles = "Admin")]
        [SwaggerOperation(
            Summary = "Admin: Deactivate Shop",
            Description = "Admin deactivates a shop (not ban)."
        )]
        [SwaggerResponse(200, "Shop deactivated successfully")]
        [SwaggerResponse(403, "Forbidden - Requires Admin role")]
        [SwaggerResponse(404, "Shop not found")]
        [SwaggerResponse(400, "Only active shops can be deactivated")]
        public async Task<IActionResult> AdminDeactivateShop(Guid id)
        {
            try
            {
                await _shopService.DeactivateShopAsync(id);
                return SuccessResponse("Shop deactivated by admin successfully");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<string>(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ErrorResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error admin deactivating shop {Id}", id);
                return ServerErrorResponse<string>("Error during admin shop deactivation");
            }
        }

        /// <summary>
        /// Admin: Ban shop
        /// </summary>
        /// <param name="id">ID of the shop to ban</param>
        /// <returns>Ban success message</returns>
        [HttpPost("admin/{id:guid}/ban")]
        //[Authorize(Roles="Admin")]
        [SwaggerOperation(
            Summary = "Admin: Ban Shop",
            Description = "Bans a shop and sets its status to Banned."
        )]
        [SwaggerResponse(200, "Shop banned successfully")]
        [SwaggerResponse(403, "Forbidden - Requires Admin role")]
        [SwaggerResponse(404, "Shop not found")]
        public async Task<IActionResult> BanShop(Guid id)
        {
            try
            {
                await _shopService.BannedShopAsync(id);
                return SuccessResponse("Shop banned successfully");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error banning shop {Id}", id);
                return ServerErrorResponse<string>("Error during shop ban");
            }
        }
    }
}
