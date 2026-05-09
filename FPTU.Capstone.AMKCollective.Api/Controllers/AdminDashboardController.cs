using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs.AdminDashboard;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    /// <summary>
    /// Provides admin dashboard analytics endpoints.
    /// </summary>
    [Route("api/v1/admin-dashboard")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    [SwaggerTag("Admin dashboard analytics endpoints. Requires Admin role.")]
    public class AdminDashboardController : BaseApiController
    {
        private readonly IAdminDashboardService _adminDashboardService;

        public AdminDashboardController(IAdminDashboardService adminDashboardService)
        {
            _adminDashboardService = adminDashboardService;
        }

        /// <summary>
        /// Gets top-level admin dashboard overview metrics.
        /// </summary>
        /// <param name="filter">Filter options for date range and dashboard scope.</param>
        /// <returns>Overview analytics payload.</returns>
        [HttpGet("overview")]
        [SwaggerOperation(
            Summary = "Get Admin Dashboard Overview",
            Description = "Returns high-level admin dashboard metrics by filter range. FE uses this endpoint to render top-level KPI cards."
        )]
        [SwaggerResponse(200, "Overview retrieved successfully")]
        [SwaggerResponse(400, "Invalid filter parameters")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - Admin role required")]
        [SwaggerResponse(500, "Internal server error")]
        public async Task<IActionResult> GetOverview([FromQuery] AdminDashboardFilterRequest filter)
        {
            try
            {
                var result = await _adminDashboardService.GetOverviewAsync(filter);
                return SuccessResponse(result, "Admin dashboard overview retrieved successfully.");
            }
            catch (ArgumentException ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Gets payment health metrics for admin monitoring.
        /// </summary>
        /// <param name="filter">Filter options for date range and dashboard scope.</param>
        /// <returns>Payment health analytics payload.</returns>
        [HttpGet("payments/health")]
        [SwaggerOperation(
            Summary = "Get Payment Health Metrics",
            Description = "Returns payment health analytics (success/failure trends and reliability indicators) for admin monitoring."
        )]
        [SwaggerResponse(200, "Payment health metrics retrieved successfully")]
        [SwaggerResponse(400, "Invalid filter parameters")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - Admin role required")]
        [SwaggerResponse(500, "Internal server error")]
        public async Task<IActionResult> GetPaymentsHealth([FromQuery] AdminDashboardFilterRequest filter)
        {
            try
            {
                var result = await _adminDashboardService.GetPaymentsHealthAsync(filter);
                return SuccessResponse(result, "Admin payment health metrics retrieved successfully.");
            }
            catch (ArgumentException ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Gets risk overview metrics for admin monitoring.
        /// </summary>
        /// <param name="filter">Filter options for date range and dashboard scope.</param>
        /// <returns>Risk overview analytics payload.</returns>
        [HttpGet("risk/overview")]
        [SwaggerOperation(
            Summary = "Get Risk Overview Metrics",
            Description = "Returns admin risk analytics summary for system-level monitoring and decision making."
        )]
        [SwaggerResponse(200, "Risk overview metrics retrieved successfully")]
        [SwaggerResponse(400, "Invalid filter parameters")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - Admin role required")]
        [SwaggerResponse(500, "Internal server error")]
        public async Task<IActionResult> GetRiskOverview([FromQuery] AdminDashboardFilterRequest filter)
        {
            try
            {
                var result = await _adminDashboardService.GetRiskOverviewAsync(filter);
                return SuccessResponse(result, "Admin risk overview metrics retrieved successfully.");
            }
            catch (ArgumentException ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Gets top shops by order count (excluding cancelled/refunded/in-cart orders).
        /// </summary>
        /// <param name="filter">Filter options for date range and dashboard scope.</param>
        /// <param name="top">Number of shops to return (default 3).</param>
        /// <returns>Top shops with order counts.</returns>
        [HttpGet("shops/top-orders")]
        [SwaggerOperation(
            Summary = "Get Top Shops By Orders",
            Description = "Returns the top N shops by order count within the selected period. Cancelled/refunded/in-cart orders are excluded."
        )]
        [SwaggerResponse(200, "Top shops retrieved successfully")]
        [SwaggerResponse(400, "Invalid filter parameters")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - Admin role required")]
        [SwaggerResponse(500, "Internal server error")]
        public async Task<IActionResult> GetTopShopsByOrders([FromQuery] AdminDashboardFilterRequest filter, [FromQuery] int top = 3)
        {
            try
            {
                var result = await _adminDashboardService.GetTopShopsByOrdersAsync(filter, top);
                return SuccessResponse(result, "Top shops by orders retrieved successfully.");
            }
            catch (ArgumentException ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }
    }
}
