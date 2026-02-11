using FPTU.Capstone.AMKCollective.Api.Controllers;
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
        /// Submits a request to withdraw funds from the wallet to a bank account.
        /// </summary>
        /// <param name="request">Withdrawal details including amount and bank information.</param>
        [HttpPost("withdraw")]
        public async Task<IActionResult> RequestWithdrawal([FromBody] WithdrawRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return ErrorResponse<object>("Validation failed", errors);
            }

            try
            {
                var userId = GetCurrentUserId();
                await _walletService.RequestWithdrawalAsync(userId, request);

                return SuccessResponse("Withdrawal request created successfully");
            }
            catch (Exception ex)
            {
                return ErrorResponse<object>(ex.Message);
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
        public async Task<IActionResult> ApproveWithdrawal(Guid paymentId)
        {
            try
            {
                var adminId = GetCurrentUserId(); // Lấy ID Admin thực hiện
                await _walletService.ApproveWithdrawalAsync(adminId, paymentId);

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
            if (!request.IsApproved && string.IsNullOrEmpty(request.Reason))
            {
                return ErrorResponse<object>("Reason is required when rejecting.");
            }

            try
            {
                var adminId = GetCurrentUserId();
                await _walletService.RejectWithdrawalAsync(adminId, paymentId, request.Reason);

                return SuccessResponse("Withdrawal rejected successfully. Funds refunded to wallet.");
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


        // Helper để lấy ID từ Token (JWT)
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
  