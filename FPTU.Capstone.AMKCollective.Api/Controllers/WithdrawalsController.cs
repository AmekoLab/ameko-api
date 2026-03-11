using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using FPTU.Capstone.AMKCollective.Application.DTOs.Withdrawal;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class WithdrawalsController : BaseApiController
    {
        private readonly IWithdrawalService _withdrawalService;

        public WithdrawalsController(IWithdrawalService withdrawalService)
        {
            _withdrawalService = withdrawalService;
        }

        /// <summary>
        /// [User/Shop] Submits a new withdrawal request.
        /// </summary>
        [HttpPost]
        [SwaggerOperation(
            Summary = "Submit withdrawal request",
            Description = "Allows Users or Shops to submit a new request to withdraw funds from their wallet balance to their bank account."
        )]
        [SwaggerResponse(200, "Withdrawal request submitted successfully", typeof(WithdrawalRequestResponseDto))]
        [SwaggerResponse(400, "Validation failed or invalid amount")]
        [SwaggerResponse(401, "Unauthorized access")]
        [SwaggerResponse(500, "Internal server error")]
        public async Task<IActionResult> CreateWithdrawal([FromBody] CreateWithdrawalRequestDto request)
        {
            if (!ModelState.IsValid)
                return ErrorResponse<object>("Validation failed", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList());

            try
            {
                var userId = GetCurrentUserId();
                var result = await _withdrawalService.CreateWithdrawalAsync(userId, request);
                return SuccessResponse(result, "Withdrawal request submitted successfully.");
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
        /// [User/Shop] Retrieves the current user's withdrawal requests history.
        /// </summary>
        [HttpGet("my")]
        [SwaggerOperation(
            Summary = "Get my withdrawals",
            Description = "Retrieves the history of all withdrawal requests made by the authenticated user."
        )]
        [SwaggerResponse(200, "Successfully retrieved user withdrawals", typeof(List<WithdrawalRequestResponseDto>))]
        [SwaggerResponse(401, "Unauthorized access")]
        [SwaggerResponse(500, "Internal server error")]
        public async Task<IActionResult> GetMyWithdrawals()
        {
            try
            {
                var userId = GetCurrentUserId();
                var results = await _withdrawalService.GetUserWithdrawalsAsync(userId);
                return SuccessResponse(results, "Retrieved user withdrawals successfully.");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// [Admin] Retrieves all pending withdrawal requests that require admin review.
        /// </summary>
        [HttpGet("pending")]
        // [Authorize(Roles = "Admin")] // Uncomment if Role-based Auth is enabled
        [SwaggerOperation(
            Summary = "Get pending withdrawals",
            Description = "Retrieves all withdrawal requests currently in the Pending status that require Admin review and processing. Admin role suggested."
        )]
        [SwaggerResponse(200, "Successfully retrieved pending withdrawals", typeof(List<WithdrawalRequestResponseDto>))]
        [SwaggerResponse(401, "Unauthorized access")]
        [SwaggerResponse(403, "Forbidden - Requires Admin role")]
        [SwaggerResponse(500, "Internal server error")]
        public async Task<IActionResult> GetPendingWithdrawals()
        {
            try
            {
                var results = await _withdrawalService.GetPendingWithdrawalsAsync();
                return SuccessResponse(results, "Retrieved pending withdrawals successfully.");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// [Admin] Approves a pending withdrawal request and attaches payment evidence.
        /// </summary>
        [HttpPost("{id}/approve")]
        // [Authorize(Roles = "Admin")] // Uncomment if Role-based Auth is enabled
        [SwaggerOperation(
            Summary = "Approve withdrawal request",
            Description = "Approves a pending withdrawal request and attaches payment evidence URL. Permamently deducts the held balance from the user wallet. Admin role suggested."
        )]
        [SwaggerResponse(200, "Withdrawal approved successfully")]
        [SwaggerResponse(400, "Validation failed or request not in Pending state")]
        [SwaggerResponse(401, "Unauthorized access")]
        [SwaggerResponse(403, "Forbidden - Requires Admin role")]
        [SwaggerResponse(404, "Withdrawal request not found")]
        [SwaggerResponse(500, "Internal server error")]
        public async Task<IActionResult> ApproveWithdrawal(Guid id, [FromBody] ApproveWithdrawalRequestDto request)
        {
            if (!ModelState.IsValid)
                return ErrorResponse<object>("Validation failed", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList());

            try
            {
                var adminId = GetCurrentUserId();
                await _withdrawalService.ApproveWithdrawalAsync(adminId, id, request);
                return SuccessResponse("Withdrawal approved successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
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
        /// [Admin] Rejects a pending withdrawal request, returning held balance to user wallet.
        /// </summary>
        [HttpPost("{id}/reject")]
        // [Authorize(Roles = "Admin")] // Uncomment if Role-based Auth is enabled
        [SwaggerOperation(
            Summary = "Reject withdrawal request",
            Description = "Rejects a pending withdrawal request. Returns the held funds back to the user's available wallet balance. Admin role suggested."
        )]
        [SwaggerResponse(200, "Withdrawal rejected successfully")]
        [SwaggerResponse(400, "Validation failed or request not in Pending state")]
        [SwaggerResponse(401, "Unauthorized access")]
        [SwaggerResponse(403, "Forbidden - Requires Admin role")]
        [SwaggerResponse(404, "Withdrawal request not found")]
        [SwaggerResponse(500, "Internal server error")]
        public async Task<IActionResult> RejectWithdrawal(Guid id, [FromBody] RejectWithdrawalRequestDto request)
        {
            if (!ModelState.IsValid)
                return ErrorResponse<object>("Validation failed", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList());

            try
            {
                var adminId = GetCurrentUserId();
                await _withdrawalService.RejectWithdrawalAsync(adminId, id, request);
                return SuccessResponse("Withdrawal rejected successfully. Funds have been returned to user wallet.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
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
    }
}
