using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs.Payment;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;
using FPTU.Capstone.AMKCollective.Application.Helpers;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    /// <summary>
    /// Handles Stripe and VNPay payment endpoints.
    /// </summary>
    [Route("api/v1/[controller]")]
    [ApiController]
    public class PaymentController : BaseApiController
    {
        private readonly IPaymentService _paymentService;
        private readonly IVnPayService _vnPayService;
        private readonly FrontendUrls _frontendUrls;

        /// <summary>
        /// Creates a new <see cref="PaymentController"/>.
        /// </summary>
        public PaymentController(IPaymentService paymentService, IVnPayService vnPayService, IOptions<FrontendUrls> frontendUrls)
        {
            _paymentService = paymentService;
            _vnPayService = vnPayService;
            _frontendUrls = frontendUrls.Value;
        }

        /// <summary>
        /// Creates a Stripe checkout session for the current user.
        /// </summary>
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

        /// <summary>
        /// Receives Stripe webhook events.
        /// </summary>
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
                await _paymentService.ProcessWebhookAsync(json, signature.ToString());

                // Webhook của Stripe chỉ cần Status 200. 
                // Có thể dùng SuccessResponse hoặc Ok() đều được
                return SuccessResponse();
            }
            catch (System.Exception ex)
            {
                return ErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// Creates a VNPay payment URL for the current user.
        /// </summary>
        [HttpPost("create-vnpay-session")]
        [Authorize]
        [SwaggerOperation(
            Summary = "Create VNPay Checkout Session",
            Description = "Creates a VNPay payment URL to redirect user for payment."
        )]
        [SwaggerResponse(200, "Session created successfully")]
        [SwaggerResponse(400, "Invalid request data")]
        [SwaggerResponse(403, "Order does not belong to current user")]
        public async Task<IActionResult> CreateVnPaySession([FromBody] CreateCheckoutSessionRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ErrorResponse<object>("Invalid request data");
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return ErrorResponse<object>("Unauthorized");

            // Truyền HttpContext vào để lấy IP của client
            var paymentUrl = await _vnPayService.CreatePaymentUrlAsync(request, userId, HttpContext);

            // Trả về một object chứa PaymentUrl cho Frontend điều hướng
            return SuccessResponse(new { PaymentUrl = paymentUrl }, "VNPay session created successfully");
        }

        /// <summary>
        /// Receives VNPay IPN callbacks from VNPay servers.
        /// </summary>
        [HttpGet("vnpay-ipn")]
        [AllowAnonymous] // Bắt buộc AllowAnonymous để server VNPay gọi vào được
        [SwaggerOperation(
            Summary = "VNPay IPN Webhook",
            Description = "Endpoint for VNPay to send asynchronous payment events (Do not call manually)."
        )]
        public async Task<IActionResult> VnPayIpn()
        {
            try
            {
                var response = await _vnPayService.ProcessIpnAsync(Request.Query);

                if (response.Success)
                {
                    // Chỗ này KHÔNG dùng SuccessResponse() của project
                    // Bạn phải trả về đúng chuẩn JSON mà Server VNPay yêu cầu
                    return Ok(new { RspCode = "00", Message = "Confirm Success" });
                }

                // Nếu chữ ký không hợp lệ
                return Ok(new { RspCode = "97", Message = "Invalid Signature" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VNPAY IPN CRITICAL] {ex.Message}");
                // Lỗi hệ thống của dự án
                return Ok(new { RspCode = "99", Message = "Unknown error" });
            }
        }
        /// <summary>
        /// Handles VNPay browser return callback and redirects to frontend result page.
        /// </summary>
        [HttpGet("vnpay-return")]
        [AllowAnonymous]
        public async Task<IActionResult> VnPayReturn()
        {
            try
            {
                var response = await _vnPayService.ProcessIpnAsync(Request.Query);

                var query = new Dictionary<string, string>
                {
                    ["provider"] = "vnpay",
                    ["paid"] = response.IsPaid ? "1" : "0",
                    ["orderId"] = response.OrderId,
                    ["transactionId"] = response.TransactionId,
                    ["responseCode"] = response.VnPayResponseCode
                };

                var targetUrl = response.IsPaid
                    ? _frontendUrls.PaymentSuccessPath
                    : _frontendUrls.PaymentCancelPath;

                return Redirect(PaymentUrlHelper.AttachQuery(targetUrl, query));
            }
            catch
            {
                return Redirect(PaymentUrlHelper.AttachQuery(_frontendUrls.PaymentCancelPath,
                                new Dictionary<string, string> { ["provider"] = "vnpay", ["paid"] = "0" }));
            }
        }
        /// <summary>
        /// Verifies VNPay return payload from frontend and confirms payment status.
        /// </summary>
        [HttpPost("vnpay-confirm")]
        [AllowAnonymous]
        public async Task<IActionResult> VnPayConfirm([FromBody] Dictionary<string, string> vnpayParams)
        {
            var queryCollection = new QueryCollection(
                vnpayParams.ToDictionary(k => k.Key, v => new StringValues(v.Value))
            );

            var response = await _vnPayService.ProcessIpnAsync(queryCollection);
            return Ok(new
            {
                success = response.Success,
                paid = response.IsPaid,
                orderId = response.OrderId,
                transactionId = response.TransactionId,
                responseCode = response.VnPayResponseCode,
                message = response.IsPaid ? "Payment successful"
                        : response.Success ? $"Payment failed (code: {response.VnPayResponseCode})"
                        : "Invalid signature"
            });
        }

        //private static string AttachQuery(string baseUrl, IDictionary<string, string> query)
        //{
        //    if (string.IsNullOrWhiteSpace(baseUrl)) return "/";

        //    var separator = baseUrl.Contains('?') ? "&" : "?";
        //    var queryString = string.Join("&", query
        //        .Where(x => !string.IsNullOrWhiteSpace(x.Value))
        //        .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));

        //    return string.IsNullOrWhiteSpace(queryString) ? baseUrl : $"{baseUrl}{separator}{queryString}";
        //}
    }
}