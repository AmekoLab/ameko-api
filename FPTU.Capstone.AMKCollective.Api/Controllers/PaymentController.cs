using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class PaymentController : BaseApiController
    {
        private readonly IPaymentService _paymentService;

        public PaymentController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        /// <summary>
        /// Stripe Webhook - Nhận thông báo kết quả thanh toán từ Stripe
        /// </summary>
        /// <remarks>
        /// API này được gọi tự động bởi server của Stripe, không gọi trực tiếp từ Frontend.
        /// Cần cấu hình URL này trong Stripe Dashboard.
        /// </remarks>
        [HttpPost("webhook")]
        [AllowAnonymous] // Webhook phải public để Stripe gọi vào được
        public async Task<IActionResult> StripeWebhook()
        {
            try
            {
                var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

                var signature = Request.Headers["Stripe-Signature"];

                await _paymentService.HandleWebhookAsync(json, signature);
               
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Xem lịch sử thanh toán của một đơn hàng
        /// </summary>
        /// <param name="orderGroupId">ID nhóm đơn hàng</param>
        /// <returns>Danh sách các lần thanh toán</returns>
        [HttpGet("history/{orderGroupId}")]
        [Authorize]
        public async Task<IActionResult> GetPaymentHistory(Guid orderGroupId)
        {
            var history = await _paymentService.GetPaymentHistoryByOrderGroupAsync(orderGroupId);
            return SuccessResponse(history);
        }
    }
}