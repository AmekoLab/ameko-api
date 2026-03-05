using FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace FPTU.Capstone.AMKCollective.Api.Controllers
{
    /// <summary>
    /// Handles warranty and return requests for orders.
    /// </summary>
    [ApiController]
    [Route("api/v1/warranty")]
    public class WarrantyController : BaseApiController
    {
        private readonly IWarrantyService _warrantyService;
        private readonly IShopService _shopService;

        public WarrantyController(IWarrantyService warrantyService, IShopService shopService)
        {
            _warrantyService = warrantyService;
            _shopService = shopService;
        }

        #region Customer Endpoints

        /// <summary>
        /// Creates a new warranty or return request for an order group.
        /// </summary>
        /// <param name="id">The OrderGroupId to cover.</param>
        /// <param name="dto">Optional: The warranty issue details (items, type, etc.).</param>
        /// <returns>The created warranty issue details.</returns>
        [Authorize]
        [HttpPost("{id}")]
        [SwaggerOperation(
            Description = "Customer submits a warranty or return request. Behavior depends on Type:\n\n" +
                          "• Type 0 (CancelRequest): Immediate refund after Admin approval.\n\n" +
                          "• Type 1 (ReturnRequest): Requires physical return of product (if Shipped/Completed) before refund.\n\n" +
                          "• Type 2 (WarrantyClaim): Immediate refund after Admin approval (No return required).\n\n" +
                          "Note: Type 1 & 2 only allowed for Completed orders. Type 0 allowed for any status. " +
                          "If OrderItemIds is null, covers all items in the OrderGroup."
        )]
        [SwaggerResponse(200, "Warranty request(s) created successfully", typeof(IEnumerable<WarrantyIssueResponse>))]
        [SwaggerResponse(400, "Validation failed (e.g. active issue exists)")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(404, "Order Group not found")]
        public async Task<IActionResult> CreateWarrantyIssue([FromRoute] Guid id, [FromBody] CreateWarrantyIssueDto? dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _warrantyService.CreateWarrantyIssueAsync(userId: userId, orderGroupId: id, dto: dto);
                return SuccessResponse(result, "Warranty request(s) created successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<object>(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        [Authorize]
        [HttpPut("{id}")]
        [SwaggerOperation(Summary = "Update an existing warranty request", Description = "Allows the customer to update details (reason, description, evidence) of a request if it is still InProgress. Type: 0 = CancelRequest, 1 = ReturnRequest, 2 = WarrantyClaim.")]
        [SwaggerResponse(200, "Warranty request updated successfully", typeof(WarrantyIssueResponse))]
        [SwaggerResponse(400, "Validation failed or invalid state")]
        [SwaggerResponse(404, "Warranty issue not found")]
        public async Task<IActionResult> UpdateWarrantyIssue([FromRoute] Guid id, [FromBody] UpdateWarrantyIssueDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _warrantyService.UpdateWarrantyIssueAsync(userId, id, dto);
                return SuccessResponse(result, "Warranty request updated successfully.");
            }
            catch (KeyNotFoundException ex) { return NotFoundResponse<object>(ex.Message); }
            catch (UnauthorizedAccessException ex) { return UnauthorizedResponse<object>(ex.Message); }
            catch (InvalidOperationException ex) { return ErrorResponse<object>(ex.Message); }
            catch (Exception ex) { return ServerErrorResponse<object>(ex.Message); }
        }

        [Authorize]
        [HttpDelete("{id}")]
        [SwaggerOperation(Summary = "Delete (cancel) a warranty request", Description = "Allows the customer to soft-delete/cancel their request if it has not been reviewed by the shop yet.")]
        [SwaggerResponse(200, "Warranty request deleted successfully")]
        [SwaggerResponse(400, "Invalid state")]
        [SwaggerResponse(404, "Warranty issue not found")]
        public async Task<IActionResult> DeleteWarrantyIssue([FromRoute] Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _warrantyService.DeleteWarrantyIssueAsync(userId, id);
                return SuccessResponse("Warranty request deleted successfully.");
            }
            catch (KeyNotFoundException ex) { return NotFoundResponse<object>(ex.Message); }
            catch (UnauthorizedAccessException ex) { return UnauthorizedResponse<object>(ex.Message); }
            catch (InvalidOperationException ex) { return ErrorResponse<object>(ex.Message); }
            catch (Exception ex) { return ServerErrorResponse<object>(ex.Message); }
        }

        /// <summary>
        /// Retrieves the current user's warranty requests.
        /// </summary>
        [Authorize]
        [HttpGet("my-requests")]
        [SwaggerOperation(Summary = "Customer: Get my warranty requests")]
        [SwaggerResponse(200, "Successfully retrieved my warranty requests", typeof(FPTU.Capstone.AMKCollective.Application.DTOs.Common.PaginatedResult<WarrantyIssueResponse>))]
        public async Task<IActionResult> GetMyWarrantyIssues([FromQuery] int currentPage = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _warrantyService.GetMyWarrantyIssuesAsync(userId, currentPage, pageSize);
                return SuccessResponse(result);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Customer withdraws an active warranty request.
        /// </summary>
        [Authorize]
        [HttpPost("withdraw/{issueId}")]
        [SwaggerOperation(Summary = "Customer: Withdraw warranty request")]
        public async Task<IActionResult> WithdrawWarranty(Guid issueId)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _warrantyService.WithdrawWarrantyAsync(userId, issueId);
                return SuccessResponse("Warranty request withdrawn successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<object>(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Customer confirms return shipment with evidence.
        /// </summary>
        /// <param name="dto">Shipment confirmation details with evidence URL.</param>
        /// <returns>Success message.</returns>
        [Authorize]
        [HttpPost("customer-ship")]
        [SwaggerOperation(
            Summary = "Confirm return shipment",
            Description = "Customer confirms they have shipped the return product. " +
                          "Requires evidence (photo/tracking). Issue must be in AwaitingReturn status."
        )]
        [SwaggerResponse(200, "Shipment confirmed successfully")]
        [SwaggerResponse(400, "Invalid state or missing evidence")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(404, "Issue not found")]
        public async Task<IActionResult> CustomerConfirmShipment([FromBody] ReturnWarrantyShipmentDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _warrantyService.CustomerConfirmShipmentAsync(userId, dto);
                return SuccessResponse("Return shipment confirmed successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<object>(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        #endregion

        #region Shop Owner Endpoints

        /// <summary>
        /// Retrieves warranty requests related to the shop owner's shop.
        /// </summary>
        [Authorize(Roles = "Shop")]
        [HttpGet("shop-requests")]
        [SwaggerOperation(Summary = "Shop: Get my shop's warranty requests")]
        [SwaggerResponse(200, "Successfully retrieved shop warranty requests", typeof(FPTU.Capstone.AMKCollective.Application.DTOs.Common.PaginatedResult<WarrantyIssueResponse>))]
        public async Task<IActionResult> GetShopWarrantyIssues([FromQuery] int currentPage = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _warrantyService.GetShopWarrantyIssuesAsync(userId, currentPage, pageSize);
                return SuccessResponse(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Shop owner approves or rejects a warranty/return request.
        /// </summary>
        /// <param name="dto">The shop review decision.</param>
        /// <returns>Success message.</returns>
        [Authorize(Roles = "Shop")]
        [HttpPost("shop-review")]
        [SwaggerOperation(
            Summary = "Shop: Review warranty request",
            Description = "Shop owner reviews the warranty/return request and either approves or rejects it. " +
                          "Issue must be in InProgress status. Shop ownership is verified."
        )]
        [SwaggerResponse(200, "Request reviewed successfully")]
        [SwaggerResponse(400, "Invalid state transition")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - Requires Shop role")]
        [SwaggerResponse(404, "Issue or order not found")]
        public async Task<IActionResult> ShopReviewWarranty([FromBody] ShopWarrantyResponseDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _warrantyService.ShopReviewWarrantyAsync(userId, dto);
                return SuccessResponse("Warranty request reviewed successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<object>(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Shop owner confirms receipt of returned product.
        /// </summary>
        /// <param name="issueId">The warranty issue ID.</param>
        /// <returns>Success message.</returns>
        [Authorize(Roles = "Shop")]
        [HttpPost("shop-confirm-receive/{issueId}")]
        [SwaggerOperation(
            Summary = "Shop: Confirm receipt of returned product",
            Description = "Shop confirms physical receipt of the returned product. " +
                          "Issue must be in Returning status. Transitions to Completed (status change only)."
        )]
        [SwaggerResponse(200, "Receipt confirmed successfully")]
        [SwaggerResponse(400, "Invalid state transition")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - Requires Shop role")]
        [SwaggerResponse(404, "Issue or order not found")]
        public async Task<IActionResult> ShopConfirmReceive(Guid issueId)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _warrantyService.ShopConfirmReceiveAsync(userId, issueId);
                return SuccessResponse("Return receipt confirmed. Issue completed.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<object>(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        #endregion

        #region Admin Endpoints

        /// <summary>
        /// Retrieves a paginated list of all warranty requests.
        /// </summary>
        /// <param name="currentPage">The page number to retrieve.</param>
        /// <param name="pageSize">The number of items per page.</param>
        /// <returns>A paginated result containing warranty requests.</returns>
        [Authorize(Roles = "Admin")]
        [HttpGet]
        [SwaggerOperation(
            Summary = "Admin: Get all warranty requests",
            Description = "Returns a paginated list of all submitted warranty and return requests."
        )]
        [SwaggerResponse(200, "Successfully retrieved list of warranty requests", typeof(FPTU.Capstone.AMKCollective.Application.DTOs.Common.PaginatedResult<WarrantyIssueResponse>))]
        [SwaggerResponse(401, "Unauthorized access")]
        [SwaggerResponse(403, "Forbidden - Requires Admin role")]
        public async Task<IActionResult> GetAllWarrantyIssues([FromQuery] int currentPage = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _warrantyService.GetAllWarrantyIssuesAsync(currentPage, pageSize);
                return SuccessResponse(result, "Warranty requests retrieved successfully.");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Admin makes a decision on a warranty/return request (approve/reject).
        /// The system automatically determines whether return is required based on Order status.
        /// </summary>
        /// <param name="dto">The admin decision details.</param>
        /// <returns>Success message.</returns>
        [Authorize(Roles = "Admin")]
        [HttpPost("admin-decision")]
        [SwaggerOperation(
            Summary = "Admin: Make warranty decision",
            Description = "Admin approves or rejects the warranty request. Behavioral logic:\n\n" +
                          "• If Type 1 (ReturnRequest) AND Order is Shipped/Completed → Status = AwaitingReturn (Wait for ship).\n\n" +
                          "• If Type 2 (WarrantyClaim) OR Type 0 (CancelRequest) → Status = Completed (Refund immediately).\n\n" +
                          "• If Order is NOT yet Shipped/Completed (for Type 0) → Refund immediately.\n\n" +
                          "Admin note is required for the decision record."
        )]
        [SwaggerResponse(200, "Decision processed successfully")]
        [SwaggerResponse(400, "Invalid state transition or refund failure")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - Requires Admin role")]
        [SwaggerResponse(404, "Issue or order not found")]
        public async Task<IActionResult> AdminDecisionWarranty([FromBody] AdminWarrantyDecisionDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                await _warrantyService.AdminDecisionWarrantyAsync(userId, dto);
                return SuccessResponse("Admin decision processed successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<object>(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        #endregion
    }
}
