using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs.Payment;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

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

        [HttpPost("create-checkout-session")]
        [Authorize] // [Fix #3] Yêu cầu đăng nhập
        [SwaggerOperation(
    Summary = "Create Checkout Session",
    Description = "Creates a Stripe checkout session for payment processing."
)]
        [SwaggerResponse(200, "Session created successfully")]
        [SwaggerResponse(400, "Invalid request data")]
        [SwaggerResponse(403, "Order does not belong to current user")]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] CreateCheckoutSessionRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ErrorResponse<object>("Invalid request data");
            }

            // [Fix #3] Validate ownership — truyền userId xuống service để kiểm tra
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return ErrorResponse<object>("Unauthorized");

            var result = await _paymentService.CreateCheckoutSessionAsync(request, userId);
            return SuccessResponse(result, "Checkout session created successfully");
        }

        [HttpPost("webhook")]
        [SwaggerOperation(
    Summary = "Stripe Webhook",
    Description = "Endpoint for Stripe to send asynchronous payment events (Do not call manually)."
)]
        [SwaggerResponse(200, "Webhook processed")]
        [SwaggerResponse(400, "Invalid signature or payload")]
        public async Task<IActionResult> StripeWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            if (!Request.Headers.TryGetValue("Stripe-Signature", out var signature))
            {
                return ErrorResponse<object>("Missing Stripe-Signature header");
            }

            try
            {
                await _paymentService.ProcessWebhookAsync(json, signature);

                // Webhook của Stripe chỉ cần Status 200. 
                // Có thể dùng SuccessResponse hoặc Ok() đều được
                return SuccessResponse();
            }
            catch (System.Exception ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
        }
    }
}