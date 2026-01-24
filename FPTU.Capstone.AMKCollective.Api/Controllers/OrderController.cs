using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using FPTU.Capstone.AMKCollective.Application.DTOs.Order;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    [Route("api/v1/orders")]
    [ApiController]
    public class OrderController : BaseApiController
    {
        private readonly IOrderService _orderService;

        public OrderController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        // =================================================================
        // 1. NHÓM API GIỎ HÀNG (CART) - Thay thế cho CartController cũ
        // =================================================================

        /// <summary>
        /// Lấy giỏ hàng hiện tại của user
        /// </summary>
        [HttpGet("cart")]
        [Authorize]
        public async Task<IActionResult> GetMyCart()
        {
            var userId = GetUserId();
            var cart = await _orderService.GetMyCartAsync(userId);
            if (cart == null) return SuccessResponse(new { items = new List<object>(), totalAmount = 0 }, "Cart empty");

            return SuccessResponse(cart);
        }

        /// <summary>
        /// Thêm sản phẩm vào giỏ hàng
        /// </summary>
        [HttpPost("cart/add")]
        [Authorize]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
        {
            var userId = GetUserId();
            try
            {
                await _orderService.AddToCartAsync(userId, request);
                return SuccessResponse("Add to cart successfully");
            }
            catch (Exception ex)
            {
                var message = ex.InnerException?.Message ?? ex.Message;
                return ErrorResponse<string>(message);
            }
        }

        /// <summary>
        /// Xóa 1 món khỏi giỏ hàng
        /// </summary>
        [HttpDelete("cart/{orderItemId}")]
        [Authorize]
        public async Task<IActionResult> RemoveFromCart(Guid orderItemId)
        {
            var userId = GetUserId();
            try
            {
                await _orderService.RemoveItemFromCartAsync(userId, orderItemId);
                return SuccessResponse("Removed item in cart");
            }
            catch (Exception ex)
            {
                return ErrorResponse<string>(ex.Message);
            }
        }

        // =================================================================
        // 2. NHÓM API THANH TOÁN (CHECKOUT)
        // =================================================================

        /// <summary>
        /// Chốt đơn (Checkout) - Chuyển từ Giỏ hàng sang Đơn hàng thật & Lấy link thanh toán
        /// </summary>
        [HttpPost("checkout")]
        [Authorize]
        public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetUserId();
            try
            {
                // Hàm này giờ sẽ lấy data từ Giỏ hàng trong DB chứ không tin tưởng Items từ Request nữa
                var result = await _orderService.CheckoutAsync(userId, request);
                return SuccessResponse(result, "Create order sucessfully, please check out");
            }
            catch (Exception ex)
            {
                return ErrorResponse<string>(ex.Message);
            }
        }

        // =================================================================
        // 3. NHÓM API LỊCH SỬ ĐƠN HÀNG (HISTORY)
        // =================================================================

        /// <summary>
        /// Lấy lịch sử mua hàng (Các đơn đã đặt)
        /// </summary>
        [HttpGet("my-orders")]
        [Authorize]
        public async Task<IActionResult> GetMyOrders()
        {
            var userId = GetUserId();
            var orders = await _orderService.GetMyOrdersAsync(userId);
            return SuccessResponse(orders);
        }

        /// <summary>
        /// Xem chi tiết một nhóm đơn hàng (Order Group)
        /// </summary>
        [HttpGet("group/{orderGroupId}")]
        [Authorize]
        public async Task<IActionResult> GetOrderGroupDetail(Guid orderGroupId)
        {
            try
            {
                var orderGroup = await _orderService.GetOrderGroupDetailAsync(orderGroupId);
                return SuccessResponse(orderGroup);
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse<string>("Order not found");
            }
        }

        [HttpPatch("cart/update")]
        [Authorize]
        public async Task<IActionResult> UpdateQuantity([FromBody] UpdateCartItemRequest request)
        {
            var userId = GetUserId();
            try
            {
                await _orderService.UpdateCartItemQuantityAsync(userId, request.OrderItemId, request.Quantity);
                return SuccessResponse("udpate quantity successfully");
            }
            catch (Exception ex)
            {
                return ErrorResponse<string>(ex.Message);
            }
        }

        // --- Helper để lấy UserId từ Token ---
        private Guid GetUserId()
        {
            var idClaim = User.FindFirst("id") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (idClaim == null) throw new UnauthorizedAccessException("Invalid Token");
            return Guid.Parse(idClaim.Value);
        }

    }
}
