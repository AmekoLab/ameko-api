using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs.Commission;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CommissionsController : BaseApiController
    {
        private readonly ICommissionService _commissionService;

        public CommissionsController(ICommissionService commissionService)
        {
            _commissionService = commissionService;
        }

        // ==========================================
        // (USER)
        // ==========================================
        /// <summary>
        /// Creates a new custom keyboard commission request.
        /// </summary>
        /// <param name="request">The payload containing the request details (title, description, budget, quantity, images, etc.).</param>
        /// <returns>Returns the ID of the newly created request.</returns>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateRequest([FromBody] CreateCommissionRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return Unauthorized();

            var result = await _commissionService.CreateRequestAsync(userId, request);

            if (!result.Success)
            {
                return ErrorResponse<object>(result.ErrorMessage);
            }

            return SuccessResponse(new { RequestId = result.RequestId }, "Request created successfully");
        }

        /// <summary>
        /// Updates a draft commission request.
        /// </summary>
        [HttpPut("{requestId}")]
        [Authorize]
        public async Task<IActionResult> UpdateRequest(Guid requestId, [FromBody] UpdateCommissionRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return Unauthorized();

            var result = await _commissionService.UpdateRequestAsync(userId, requestId, request);
            if (!result.Success)
            {
                return ErrorResponse<object>(result.ErrorMessage);
            }

            return SuccessResponse<object>("Request updated successfully");
        }
        /// <summary>
        /// Retrieves a list of all commission requests created by the currently logged-in user.
        /// </summary>
        /// <returns>A list of commission requests with their current statuses.</returns>
        [HttpGet("my-requests")]
        [Authorize]
        public async Task<IActionResult> GetMyRequests()
        {
            var userId = GetCurrentUserId();
            var requests = await _commissionService.GetUserRequestsAsync(userId);
            return SuccessResponse(requests, "Requests retrieved successfully");
        }
        /// <summary>
        /// Retrieves the details of a specific commission request.
        /// NOTE FOR FE: This API handles bidding privacy. If called by the Customer, it returns ALL quotes. 
        /// If called by a Shop, it ONLY returns the quote submitted by that specific Shop.
        /// </summary>
        /// <param name="id">The ID of the commission request.</param>
        /// <returns>Detailed information of the request including the list of quotes.</returns>
        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetRequestDetail(Guid id)
        {
            var userId = GetCurrentUserId();
            var request = await _commissionService.GetRequestDetailAsync(id, userId);
            if (request == null) return NotFoundResponse<object>("Request not found");

            return SuccessResponse(request, "Request retrieved successfully");
        }
        /// <summary>
        /// Accepts a specific quote submitted by a shop. 
        /// This action closes the commission request and automatically generates an Order for checkout.
        /// </summary>
        /// <param name="quoteId">The ID of the quote to be accepted.</param>
        /// <returns>Returns the generated OrderId. FE should use this OrderId to redirect the user to the Checkout page.</returns>
        [HttpPost("quotes/{quoteId}/accept")]
        [Authorize]
        public async Task<IActionResult> AcceptQuote(Guid quoteId)
        {
            var userId = GetCurrentUserId();
            var result = await _commissionService.AcceptQuoteAsync(userId, quoteId);

            if (!result.Success)
            {
                return ErrorResponse<object>(result.ErrorMessage);
            }

            return SuccessResponse(new { OrderId = result.OrderId }, "Quotation finalized successfully. Redirecting to the cart...");
        }
        /// <summary>
        /// Cancels a commission request.
        /// Only allowed if the request has not been completed (no quote accepted yet).
        /// </summary>
        /// <param name="requestId">The ID of the commission request to cancel.</param>
        /// <returns>Success or error message.</returns>
        [HttpPost("{requestId}/cancel")]
        [Authorize]
        public async Task<IActionResult> CancelRequest(Guid requestId)
        {
            var userId = GetCurrentUserId();
            var result = await _commissionService.CancelRequestAsync(userId, requestId);

            if (!result.Success) return ErrorResponse<object>(result.ErrorMessage);

            return SuccessResponse<object>("The request has been successfully canceled.");
        }

        /// <summary>
        /// Customer action: Publish a rejected or pending targeted request to the public pool.
        /// </summary>
        [HttpPost("{requestId}/publish")]
        [Authorize]
        public async Task<IActionResult> PublishToPool(Guid requestId)
        {
            var userId = GetCurrentUserId();
            var result = await _commissionService.PublishToPoolAsync(userId, requestId);

            if (!result.Success) return ErrorResponse<object>(result.ErrorMessage);

            return SuccessResponse<object>("Successfully published to the public pool.");
        }
        // ==========================================
        // SHOP
        // ==========================================
        /// <summary>
        /// Retrieves a list of all open commission requests available in the public pool.
        /// Shops use this API to find potential jobs to bid on.
        /// </summary>
        /// <returns>A list of open commission requests.</returns>
        [HttpGet("pool")]
        [Authorize] 
        public async Task<IActionResult> GetOpenPoolRequests()
        {
            var requests = await _commissionService.GetOpenPoolRequestsAsync();
            return SuccessResponse(requests, "Open pool requests retrieved successfully");
        }
        /// <summary>
        /// Submits a new quotation (bid) for a specific commission request from the public pool or targeted list.
        /// </summary>
        /// <param name="requestId">The ID of the commission request.</param>
        /// <param name="request">The quotation details (price, estimated days, shop notes).</param>
        /// <returns>Success or error message.</returns>
        [HttpPost("{requestId}/quotes")]
        [Authorize] 
        public async Task<IActionResult> SubmitQuote(Guid requestId, [FromBody] SubmitQuoteRequest request)
        {
            var shopUserId = GetCurrentUserId();
            if (shopUserId == Guid.Empty) return Unauthorized();

            var result = await _commissionService.SubmitQuoteAsync(shopUserId, requestId, request);

            if (!result.Success)
            {
                return ErrorResponse<object>(result.ErrorMessage);
            }

            return SuccessResponse<object>("Quotation submitted successfully.");
        }
        /// <summary>
        /// Updates an existing quotation.
        /// Only allowed if the customer has not yet made a decision (PendingUserDecision status).
        /// </summary>
        /// <param name="quoteId">The ID of the quote to update.</param>
        /// <param name="request">The updated quotation details.</param>
        /// <returns>Success or error message.</returns>
        [Obsolete("Updating a quotation is not supported. Revoke and submit a new quote instead.")]
        [HttpPut("quotes/{quoteId}")]
        [Authorize]
        public async Task<IActionResult> UpdateQuote(Guid quoteId, [FromBody] SubmitQuoteRequest request)
        {
            var shopUserId = GetCurrentUserId();
            var result = await _commissionService.UpdateQuoteAsync(shopUserId, quoteId, request);

            if (!result.Success) return ErrorResponse<object>(result.ErrorMessage);

            return SuccessResponse<object>("Quotation updated successfully.");
        }
        /// <summary>
        /// Revokes (withdraws) a quotation previously submitted by the shop.
        /// Only allowed if the customer has not yet accepted it.
        /// </summary>
        /// <param name="quoteId">The ID of the quote to revoke.</param>
        /// <returns>Success or error message.</returns>
        [HttpPost("quotes/{quoteId}/revoke")]
        [Authorize]
        public async Task<IActionResult> RevokeQuote(Guid quoteId)
        {
            var shopUserId = GetCurrentUserId();
            var result = await _commissionService.RevokeQuoteAsync(shopUserId, quoteId);

            if (!result.Success) return ErrorResponse<object>(result.ErrorMessage);

            return SuccessResponse<object>("Quotation withdrawn successfully.");
        }
        /// <summary>
        /// Retrieves a list of commission requests that were specifically targeted (sent directly) to the logged-in shop by customers.
        /// </summary>
        /// <returns>A list of targeted commission requests.</returns>
        [HttpGet("shop/targeted")]
        [Authorize]
        public async Task<IActionResult> GetShopTargetedRequests()
        {
            var shopUserId = GetCurrentUserId();
            var requests = await _commissionService.GetShopTargetedRequestsAsync(shopUserId);
            return SuccessResponse(requests, "Targeted requests retrieved successfully");
        }
        /// <summary>
        /// Retrieves the history of all quotations submitted by the logged-in shop.
        /// Used by the shop to track the status of their bids (Pending, Accepted, Rejected, Revoked).
        /// </summary>
        /// <returns>A list of quotations submitted by the shop.</returns>
        [HttpGet("shop/quotes")]
        [Authorize]
        public async Task<IActionResult> GetShopQuotes()
        {
            var shopUserId = GetCurrentUserId();
            var quotes = await _commissionService.GetShopQuotesAsync(shopUserId);
            return SuccessResponse(quotes, "Shop quotes retrieved successfully");
        }

        /// <summary>
        /// Shop action: Reject a commission request targeted specifically at them.
        /// </summary>
        [HttpPost("{requestId}/reject-target")]
        [Authorize]
        public async Task<IActionResult> RejectTargetedRequest(Guid requestId)
        {
            var shopUserId = GetCurrentUserId();
            var result = await _commissionService.RejectTargetedRequestAsync(shopUserId, requestId);

            if (!result.Success) return ErrorResponse<object>(result.ErrorMessage);

            return SuccessResponse<object>("Successfully rejected the targeted request.");
        }
    }
}
   
