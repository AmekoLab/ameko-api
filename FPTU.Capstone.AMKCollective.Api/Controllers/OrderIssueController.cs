using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    [Route("api/v1/order-issues")]
    [ApiController]
    [Authorize]
    public class OrderIssueController : BaseApiController
    {
        private readonly IOrderIssueService _orderIssueService;


        public OrderIssueController(IOrderIssueService orderIssueService)
        {
            _orderIssueService = orderIssueService;
        }

        /// <summary>
        /// Get Issue by Order ID (Customer & Shop View)
        /// </summary>
        /// <remarks>
        /// **Description:** Checks if a specific Order has an active cancellation/issue request.
        /// 
        /// **Frontend usage:** /// Call this API on the "Order Details" page. 
        /// - If data is `null`: The order is normal.
        /// - If data exists: Display a "Cancellation Requested" warning banner using the returned data.
        /// </remarks>
        [HttpGet("order/{orderId}")]
        public async Task<IActionResult> GetIssueByOrderId(Guid orderId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _orderIssueService.GetIssueByOrderIdAsync(userId, orderId);

                return SuccessResponse(result, "Order issue retrieved successfully.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Get Customer's Issue History (Customer View)
        /// </summary>
        /// <remarks>
        /// **Description:** Retrieves a paginated list of all cancellation/issue requests created by the current customer.
        /// 
        /// **Frontend usage:** /// Use this API for the "My Cancellation & Return History" list page.
        /// </remarks>
        [HttpGet("me")]
        public async Task<IActionResult> GetMyIssues([FromQuery] OrderIssueFilterRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _orderIssueService.GetMyIssuesAsync(userId, request);
                return SuccessResponse(result, "Customer issues retrieved successfully.");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Get Shop's Issue Management List (Shop View)
        /// Description: Retrieves a paginated list of order issues that belong to the current shop and need processing.
        /// Frontend usage: Use this API for the "Manage Cancellations" page on the Shop Dashboard.
        /// </summary>
        [HttpGet("shop")]
        public async Task<IActionResult> GetShopIssues([FromQuery] OrderIssueFilterRequest request)
        {
            try
            {
                var shopOwnerId = GetCurrentUserId();
                var result = await _orderIssueService.GetShopIssuesAsync(shopOwnerId, request);

                return SuccessResponse(result, "Shop issues retrieved successfully.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Get Issue Detail (Shared View)
        /// Description: Retrieves the full details of a specific order issue.
        /// Frontend usage: Call this API when the user clicks on a specific issue from the list to view its details.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetIssueDetail(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _orderIssueService.GetIssueDetailAsync(userId, id);
                return SuccessResponse(result, "Issue details retrieved successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Get Issue Timeline Logs (Shared View)
        /// Description: Retrieves the history/timeline logs of a specific order issue.
        /// Frontend usage: Use this API to render the vertical progress timeline (e.g., "Customer requested -> Shop rejected -> System auto-processed").
        /// </summary>
        [HttpGet("{id}/logs")]
        public async Task<IActionResult> GetIssueLogs(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _orderIssueService.GetIssueLogsAsync(userId, id);
                return SuccessResponse(result, "Issue logs retrieved successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Withdraw a Cancellation Request (Customer View)
        /// Description: Allows a customer to cancel their own cancellation request if they change their mind.
        /// </summary>
        [HttpPost("{id}/cancel-request")]
        public async Task<IActionResult> CancelIssueRequest(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _orderIssueService.CancelIssueRequestAsync(userId, id);
                return SuccessResponse<object>(null, "Issue request cancelled successfully.");
            }
            catch (Exception ex)
            {
                // (Bắt các Exception tương tự như code cũ)
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Get All Issues (Admin View)
        /// Description: Retrieves a paginated list of ALL issues across the platform for Admin monitoring.
        /// </summary>
        [HttpGet("admin")]
        // [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllIssuesForAdmin([FromQuery] OrderIssueFilterRequest request)
        {
            try
            {
                // Ở API của Admin, ta không cần lấy UserId để check nữa, gọi thẳng Service
                var result = await _orderIssueService.GetAllIssuesForAdminAsync(request);

                return SuccessResponse(result, "All platform issues retrieved successfully.");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }
    }
}