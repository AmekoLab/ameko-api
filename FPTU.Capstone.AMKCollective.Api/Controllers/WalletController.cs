using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using FPTU.Capstone.AMKCollective.Application.DTOs.Payment;
using FPTU.Capstone.AMKCollective.Application.DTOs.Wallet;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class WalletController : BaseApiController
    {
        private readonly IWalletService _walletService;

        public WalletController(IWalletService walletService)
        {
            _walletService = walletService;
        }

        /// <summary>
        /// Retrieves the current user's wallet details, including balance and held balance.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMyWallet()
        {
            try
            {
                var userId = GetCurrentUserId();
                var wallet = await _walletService.GetWalletByUserIdAsync(userId);

                if (wallet == null)
                {
                    // Tùy chọn: Có thể tự động tạo ví nếu chưa có, hoặc trả về lỗi
                    // Ở đây mình trả về lỗi để FE biết xử lý
                    return NotFoundResponse<WalletResponse>("Wallet not found or not activated.");
                }

                return SuccessResponse(wallet, "Get wallet successfully");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<WalletResponse>(ex.Message);
            }
        }

        /// <summary>
        /// Retrieves the transaction history for the current user with support for filtering and pagination.
        /// </summary>
        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions([FromQuery] PaymentFilterRequest filter)
        {
            try
            {
                var userId = GetCurrentUserId();

                // Bắt buộc ghi đè UserId trong filter bằng ID của người đang đăng nhập
                // Để ngăn user A xem trộm giao dịch của user B bằng cách gửi ?userId={id_cua_B}
                filter.UserId = userId;

                // Gọi hàm Service mới (GetTransactionsByFilterAsync)
                var result = await _walletService.GetTransactionsByFilterAsync(filter);

                return SuccessResponse(result, "Get transaction history successfully");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Submits a withdrawal request. Requires Wallet PIN and sufficient balance.
        /// </summary>
        /// <remarks>
        /// This endpoint allows a Shop to request a withdrawal from their wallet to their registered bank account.
        /// <br/>
        /// <b>Prerequisites:</b>
        /// <ul>
        /// <li>User must be a Shop owner.</li>
        /// <li>Shop must have valid Bank Information in profile.</li>
        /// <li>Wallet PIN must be set up.</li>
        /// <li>Balance must cover the withdrawal amount + fee.</li>
        /// </ul>
        /// </remarks>
        /// <param name="request">Contains the amount to withdraw and the 6-digit security PIN.</param>
        /// <returns>Success message if the request is queued for Admin approval.</returns>
        /// <response code="200">Withdrawal request submitted successfully.</response>
        /// <response code="400">Validation failed (e.g., Insufficient balance, missing bank info, invalid amount).</response>
        /// <response code="401">Unauthorized (e.g., Invalid Wallet PIN).</response>
        /// <response code="500">Internal server error.</response>
        [HttpPost("withdraw")]
        [ProducesResponseType(typeof(ApiResponse<string>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 401)]
        public async Task<IActionResult> RequestWithdrawal([FromBody] WithdrawRequest request)
        {
            if (!ModelState.IsValid)
                return ErrorResponse<object>("Validation failed", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList());

            try
            {
                var userId = GetCurrentUserId();
                await _walletService.RequestWithdrawalAsync(userId, request);
                return SuccessResponse("Withdrawal request submitted successfully. Please wait for Admin approval.");
            }
            catch (UnauthorizedAccessException ex) // PIN Error
            {
                // Ensure your Service throws: "Incorrect Wallet PIN."
                return ErrorResponse<object>(ex.Message);
            }
            catch (InvalidOperationException ex) // Business Logic Error
            {
                // Ensure your Service throws: "Insufficient balance." or "Bank info missing."
                return ErrorResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Manually initializes a new wallet for the current user (Use this if the wallet was not created upon registration).
        /// </summary>
        [HttpPost("init")]
        public async Task<IActionResult> InitializeWallet()
        {
            try
            {
                var userId = GetCurrentUserId();
                await _walletService.CreateWalletAsync(userId);
                return SuccessResponse("Wallet initialized successfully");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// [Admin] Retrieves all system transactions with advanced filtering and pagination options.
        /// </summary>
        [HttpGet("admin/transactions")]
        [SwaggerOperation(Summary = "Get All Transactions (Filter & Paging)")]
        public async Task<IActionResult> GetAllTransactions([FromQuery] PaymentFilterRequest filter)
        {
            try
            {
                var result = await _walletService.GetTransactionsByFilterAsync(filter);
                return SuccessResponse(result, "Get transactions successfully");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// [Admin] Retrieves a list of pending withdrawal requests that require approval.
        /// </summary>
        [HttpGet("admin/withdrawals/pending")]
        public async Task<IActionResult> GetPendingWithdrawals()
        {
            try
            {
                // Tái sử dụng hàm Filter, chỉ set cứng Type và Status
                var filter = new PaymentFilterRequest
                {
                    Type = PaymentType.Withdrawal,
                    Status = PaymentStatus.Pending,
                    PageSize = 100, // Lấy nhiều chút
                    SortBy = "CreatedAt",
                    IsAscending = true // Cũ nhất lên đầu để xử lý trước
                };

                var result = await _walletService.GetTransactionsByFilterAsync(filter);
                return SuccessResponse(result, "Get pending withdrawals successfully");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// [Admin] Approves a withdrawal request.
        /// Description: Confirms that the fund transfer is successful externally and updates the transaction status to 'Paid'.
        /// </summary>
        [HttpPost("admin/withdrawals/{paymentId}/approve")]
        public async Task<IActionResult> ApproveWithdrawal(Guid paymentId, [FromBody] WithdrawalActionRequest request)
        {
            try
            {
                var adminId = GetCurrentUserId(); // Lấy ID Admin thực hiện
                await _walletService.ApproveWithdrawalAsync(adminId, paymentId, request);

                return SuccessResponse("Withdrawal approved successfully. Status updated to Paid.");
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
        /// [Admin] Rejects a withdrawal request.
        /// Description: Marks the request as rejected and refunds the amount back to the shop's wallet balance.
        /// </summary>
        [HttpPost("admin/withdrawals/{paymentId}/reject")]
        public async Task<IActionResult> RejectWithdrawal(Guid paymentId, [FromBody] WithdrawalActionRequest request)
        {
            //if (string.IsNullOrEmpty(request.Reason))
            //{
            //    return ErrorResponse<object>("Reason is required when rejecting.");
            //}

            try
            {
                var adminId = GetCurrentUserId();
                await _walletService.RejectWithdrawalAsync(adminId, paymentId, request);

                return SuccessResponse("Withdrawal rejected successfully. Funds refunded to wallet.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
            }
            catch (ArgumentException ex) // Bắt lỗi validate (thiếu lý do)
            {
                return ErrorResponse<object>(ex.Message);
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
        /// [Admin] Manually adjusts a user's wallet balance (Add or Deduct funds).
        /// Description: Creates a transaction record (ManualAdjustment) and updates the balance immediately.
        /// Use cases: Compensation, Penalty, or correcting system errors.
        /// </summary>
        /// <param name="request">
        /// Contains:
        /// - UserId: The target user's ID.
        /// - Amount: Positive value to ADD, Negative value to DEDUCT.
        /// - Reason: The reason for this adjustment (Required for audit).
        /// </param>
        /// <returns>Success message upon completion.</returns>
        [HttpPost("admin/adjust-balance")]
        // [Authorize(Roles = "Admin")] // Uncomment this in production
        public async Task<IActionResult> AdjustBalance([FromBody] AdjustBalanceRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return ErrorResponse<object>("Validation failed", errors);
            }

            try
            {
                var adminId = GetCurrentUserId(); // Get ID of the Admin performing this action

                await _walletService.AdjustBalanceAsync(adminId, request);

                string actionType = request.Amount >= 0 ? "credited to" : "deducted from";
                return SuccessResponse($"Successfully {actionType} the wallet. Amount: {Math.Abs(request.Amount):N0} VND.");
            }
            catch (KeyNotFoundException ex)
            {
                // Returns 404 if User or Wallet is not found
                return NotFoundResponse<object>(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                // Returns 400 for logic errors (e.g., Insufficient balance to deduct)
                return ErrorResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }
        /// <summary>
        /// Checks if the current user has set up a wallet PIN.
        /// </summary>
        [HttpGet("pin/status")]
        public async Task<IActionResult> CheckPinStatus()
        {
            try
            {
                var userId = GetCurrentUserId();
                var hasPin = await _walletService.IsPinCreatedAsync(userId);
                return SuccessResponse(new { HasPin = hasPin }, "Check PIN status successfully.");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Sets up a new PIN for the wallet (First time only). Requires current login password.
        /// </summary>
        [HttpPost("pin/setup")]
        public async Task<IActionResult> SetupPin([FromBody] SetupWalletPinRequest request)
        {
            if (!ModelState.IsValid)
                return ErrorResponse<object>("Validation failed", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList());

            try
            {
                var userId = GetCurrentUserId();
                await _walletService.SetupPinAsync(userId, request);
                return SuccessResponse("Wallet PIN set up successfully.");
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
        /// Changes the existing Wallet PIN. Requires old PIN.
        /// </summary>
        [HttpPut("pin/change")]
        public async Task<IActionResult> ChangePin([FromBody] ChangeWalletPinRequest request)
        {
            if (!ModelState.IsValid)
                return ErrorResponse<object>("Validation failed");

            try
            {
                var userId = GetCurrentUserId();
                await _walletService.ChangePinAsync(userId, request);
                return SuccessResponse("Wallet PIN changed successfully.");
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
        /// Sends an OTP to the user's email to reset the Wallet PIN.
        /// </summary>
        [HttpPost("pin/forgot")]
        public async Task<IActionResult> ForgotPin()
        {
            try
            {
                var userId = GetCurrentUserId();
                await _walletService.SendPinResetCodeAsync(userId);
                return SuccessResponse("OTP has been sent to your email. Please check inbox/spam.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Resets the Wallet PIN using the OTP received via email.
        /// </summary>
        [HttpPost("pin/reset")]
        public async Task<IActionResult> ResetPin([FromBody] ResetWalletPinRequest request)
        {
            if (!ModelState.IsValid)
                return ErrorResponse<object>("Validation failed", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList());

            try
            {
                var userId = GetCurrentUserId();
                await _walletService.ResetPinWithOtpAsync(userId, request);
                return SuccessResponse("Wallet PIN has been reset successfully.");
            }
            catch (ArgumentException ex)
            {
                return ErrorResponse<object>(ex.Message); // Lỗi do sai OTP hoặc hết hạn
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Tạo yêu cầu nạp tiền vào ví (Deposit). Trả về URL thanh toán Stripe.
        /// </summary>
        [HttpPost("deposit")]
        public async Task<IActionResult> Deposit([FromBody] DepositRequest request)
        {
            if (!ModelState.IsValid)
                return ErrorResponse<object>("Dữ liệu không hợp lệ.", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList());

            try
            {
                var userId = GetCurrentUserId();
               
                var paymentUrl = await _walletService.CreateDepositTransactionAsync(userId, request);

                // Trả về URL để FE redirect người dùng sang Stripe
                return SuccessResponse(new { Url = paymentUrl }, "Top-up session created successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                // Log lỗi nếu cần
                return ServerErrorResponse<object>($"System error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves the wallet's financial overview (Balance, Revenue, Withdrawn amount).
        /// </summary>
        /// <remarks>
        /// Use this endpoint to populate the Shop's Dashboard with key financial metrics.
        /// <br/>
        /// <b>Fields returned:</b>
        /// <ul>
        /// <li><b>AvailableBalance:</b> Current funds available for withdrawal.</li>
        /// <li><b>HeldBalance:</b> Funds currently locked (pending orders).</li>
        /// <li><b>TotalRevenue:</b> Total earnings from completed orders + deposits.</li>
        /// <li><b>TotalWithdrawn:</b> Total amount successfully withdrawn to bank.</li>
        /// <li><b>PendingWithdrawal:</b> Amount currently waiting for Admin approval.</li>
        /// <li><b>ThisMonthRevenue:</b> Revenue calculated from the 1st of the current month.</li>
        /// </ul>
        /// </remarks>
        /// <returns>A summary object containing financial statistics.</returns>
        /// <response code="200">Returns the wallet statistics successfully.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet("statistics")]
        [ProducesResponseType(typeof(ApiResponse<WalletStatisticsResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<IActionResult> GetWalletStatistics()
        {
            try
            {
                var userId = GetCurrentUserId();
                var stats = await _walletService.GetWalletStatisticsAsync(userId);
                return SuccessResponse(stats, "Wallet statistics retrieved successfully.");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Retrieves a detailed list of transactions currently held (Held Balance Breakdown).
        /// </summary>
        /// <remarks>
        /// Returns a breakdown of the <b>HeldBalance</b>. 
        /// <br/>
        /// These are funds from orders that are <b>SalesPending</b> (not yet completed/delivered).
        /// Use this to show the Shop Owner <i>why</i> their money is being held and which orders are responsible.
        /// </remarks>
        /// <returns>List of held transactions with associated Order status.</returns>
        /// <response code="200">Returns the list of held transactions.</response>
        /// <response code="500">Internal server error.</response>
        [HttpGet("held-transactions")]
        [ProducesResponseType(typeof(ApiResponse<List<HeldTransactionResponse>>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<IActionResult> GetHeldTransactions()
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _walletService.GetHeldTransactionsAsync(userId);
                return SuccessResponse(result, "Held transactions retrieved successfully.");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }


        // Helper để lấy ID từ Token (JWT)
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
  