using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs.Wallet;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        /// Lấy thông tin ví của người dùng hiện tại (Số dư, Số dư chờ duyệt)
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
        /// Lấy lịch sử giao dịch (Payment Log) của người dùng hiện tại
        /// </summary>
        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions()
        {
            try
            {
                var userId = GetCurrentUserId();
                var transactions = await _walletService.GetTransactionsAsync(userId);

                return SuccessResponse(transactions, "Get transaction history successfully");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<List<WalletTransactionResponse>>(ex.Message);
            }
        }

        /// <summary>
        /// Yêu cầu rút tiền về tài khoản ngân hàng
        /// </summary>
        /// <param name="request">Thông tin rút tiền (Số tiền, Ngân hàng)</param>
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
        /// Kích hoạt ví mới cho người dùng (Thường gọi khi đăng ký, nhưng để endpoint này để backup)
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
  