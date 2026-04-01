using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs.ShopDashboard;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    /// <summary>
    /// Provides shop dashboard analytics endpoints.
    /// </summary>
    [Route("api/v1/shop-dashboard")]
    [ApiController]
    [Authorize(Roles = "Shop")]
    [SwaggerTag("Shop dashboard analytics endpoints. Requires Shop role.")]
    public class ShopDashboardController : BaseApiController
    {
        private readonly IShopDashboardService _shopDashboardService;

      
        public ShopDashboardController(IShopDashboardService shopDashboardService)
        {
            _shopDashboardService = shopDashboardService;
        }

        /// <summary>
        /// Gets aggregated customer behavior overview for the current shop.
        /// </summary>
        /// <param name="filter">Filter options for date range and dashboard scope.</param>
        /// <returns>Customer behavior overview analytics payload.</returns>
        [HttpGet("customers/overview")]
        [SwaggerOperation(
            Summary = "Get Customer Behavior Overview",
            Description = "Returns aggregated customer behavior overview for the current shop by filter range."
        )]
        [SwaggerResponse(200, "Customer behavior overview retrieved successfully")]
        [SwaggerResponse(400, "Invalid filter parameters")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - Shop role required")]
        [SwaggerResponse(404, "Shop not found")]
        [SwaggerResponse(500, "Internal server error")]
        public async Task<IActionResult> GetCustomerOverview([FromQuery] ShopBehaviorFilterRequest filter)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _shopDashboardService.GetCustomerBehaviorOverviewAsync(userId, filter);
                return SuccessResponse(result, "Customer behavior overview retrieved successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
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
        /// Gets customer behavior trend data for the current shop.
        /// </summary>
        /// <param name="filter">Filter options for date range and dashboard scope.</param>
        /// <returns>Customer trend analytics payload.</returns>
        [HttpGet("customers/trend")]
        [SwaggerOperation(
            Summary = "Get Customer Behavior Trend",
            Description = "Returns customer behavior trend data over time for chart rendering in FE dashboard."
        )]
        [SwaggerResponse(200, "Customer behavior trend retrieved successfully")]
        [SwaggerResponse(400, "Invalid filter parameters")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - Shop role required")]
        [SwaggerResponse(404, "Shop not found")]
        [SwaggerResponse(500, "Internal server error")]
        public async Task<IActionResult> GetCustomerTrend([FromQuery] ShopBehaviorFilterRequest filter)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _shopDashboardService.GetCustomerBehaviorTrendAsync(userId, filter);
                return SuccessResponse(result, "Customer behavior trend retrieved successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
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
        /// Gets top spending customers for the current shop.
        /// </summary>
        /// <param name="filter">Filter options for date range and dashboard scope.</param>
        /// <returns>Top spenders analytics payload.</returns>
        [HttpGet("customers/top-spenders")]
        [SwaggerOperation(
            Summary = "Get Top Spenders",
            Description = "Returns the top spending customers for the current shop in the selected filter period."
        )]
        [SwaggerResponse(200, "Top spenders retrieved successfully")]
        [SwaggerResponse(400, "Invalid filter parameters")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - Shop role required")]
        [SwaggerResponse(404, "Shop not found")]
        [SwaggerResponse(500, "Internal server error")]
        public async Task<IActionResult> GetTopSpenders([FromQuery] ShopBehaviorFilterRequest filter)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _shopDashboardService.GetTopSpendersAsync(userId, filter);
                return SuccessResponse(result, "Top spenders retrieved successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
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
        /// Gets churn-risk customers for retention actions.
        /// </summary>
        /// <param name="filter">Filter options for date range and dashboard scope.</param>
        /// <returns>Churn-risk customer analytics payload.</returns>
        [HttpGet("customers/churn-risk")]
        [SwaggerOperation(
            Summary = "Get Churn Risk Customers",
            Description = "Returns customers at churn risk for retention actions in shop dashboard."
        )]
        [SwaggerResponse(200, "Churn risk customer list retrieved successfully")]
        [SwaggerResponse(400, "Invalid filter parameters")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - Shop role required")]
        [SwaggerResponse(404, "Shop not found")]
        [SwaggerResponse(500, "Internal server error")]
        public async Task<IActionResult> GetChurnRiskCustomers([FromQuery] ShopBehaviorFilterRequest filter)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _shopDashboardService.GetChurnRiskCustomersAsync(userId, filter);
                return SuccessResponse(result, "Churn risk customer list retrieved successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
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
        /// Gets purchase frequency metrics for repeat-purchase analysis.
        /// </summary>
        /// <param name="filter">Filter options for date range and dashboard scope.</param>
        /// <returns>Purchase frequency analytics payload.</returns>
        [HttpGet("customers/purchase-frequency")]
        [SwaggerOperation(
            Summary = "Get Purchase Frequency Metrics",
            Description = "Returns customer purchase frequency metrics for repeat-purchase analysis."
        )]
        [SwaggerResponse(200, "Purchase frequency metrics retrieved successfully")]
        [SwaggerResponse(400, "Invalid filter parameters")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - Shop role required")]
        [SwaggerResponse(404, "Shop not found")]
        [SwaggerResponse(500, "Internal server error")]
        public async Task<IActionResult> GetPurchaseFrequency([FromQuery] ShopBehaviorFilterRequest filter)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _shopDashboardService.GetPurchaseFrequencyAsync(userId, filter);
                return SuccessResponse(result, "Purchase frequency metrics retrieved successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
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
        /// Gets conversion summary metrics for the current shop.
        /// </summary>
        /// <param name="filter">Filter options for date range and dashboard scope.</param>
        /// <returns>Conversion summary analytics payload.</returns>
        [HttpGet("customers/conversion")]
        [SwaggerOperation(
            Summary = "Get Conversion Summary",
            Description = "Returns conversion metrics from visits/interactions to completed purchases for the current shop."
        )]
        [SwaggerResponse(200, "Conversion summary retrieved successfully")]
        [SwaggerResponse(400, "Invalid filter parameters")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - Shop role required")]
        [SwaggerResponse(404, "Shop not found")]
        [SwaggerResponse(500, "Internal server error")]
        public async Task<IActionResult> GetConversionSummary([FromQuery] ShopBehaviorFilterRequest filter)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _shopDashboardService.GetConversionSummaryAsync(userId, filter);
                return SuccessResponse(result, "Conversion summary retrieved successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
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
