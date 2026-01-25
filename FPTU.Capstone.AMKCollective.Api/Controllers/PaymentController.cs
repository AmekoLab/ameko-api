using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs.Payment;
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

        [HttpPost("create-checkout-session")]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] CreateCheckoutSessionRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ErrorResponse<object>("Invalid request data");
            }

            var result = await _paymentService.CreateCheckoutSessionAsync(request);
            return SuccessResponse(result, "Checkout session created successfully");
        }

        [HttpPost("webhook")]
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
                // Có thể dùng SuccessResponse hoặc Ok() đều được, nhưng Ok() nhẹ hơn.
                return SuccessResponse();
            }
            catch (System.Exception ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
        }
    }
}