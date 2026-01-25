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

        // 1. Checkout
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _orderService.CheckoutAsync(userId, request);
            return SuccessResponse(result, "Checkout initiated successfully");
        }

        // 2. Thêm vào giỏ
        [HttpPost("cart")]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
        {
            var userId = GetCurrentUserId();
            await _orderService.AddToCartAsync(userId, request);
            return SuccessResponse("Item added to cart successfully");
        }

        // 3. Lấy giỏ hàng
        [HttpGet("cart")]
        public async Task<IActionResult> GetMyCart()
        {
            var userId = GetCurrentUserId();
            var cart = await _orderService.GetMyCartAsync(userId);
            return SuccessResponse(cart, "Cart retrieved successfully");
        }

        // 4. Xóa item khỏi giỏ
        [HttpDelete("cart/{itemId}")]
        public async Task<IActionResult> RemoveFromCart(Guid itemId)
        {
            var userId = GetCurrentUserId();
            await _orderService.RemoveItemFromCartAsync(userId, itemId);

            return SuccessResponse("Item removed from cart");
        }

        // 5. Cập nhật số lượng
        [HttpPut("cart/{itemId}")]
        public async Task<IActionResult> UpdateCartItem(Guid itemId, [FromBody] UpdateCartItemRequest request)
        {
            var userId = GetCurrentUserId();
            await _orderService.UpdateCartItemQuantityAsync(userId, itemId, request.Quantity);

            return SuccessResponse("Cart item updated successfully");
        }

        // 6. Lấy lịch sử đơn hàng
        [HttpGet("my-orders")]
        public async Task<IActionResult> GetMyOrders()
        {
            var userId = GetCurrentUserId();
            var orders = await _orderService.GetMyOrdersAsync(userId);

            return SuccessResponse(orders, "Orders retrieved successfully");
        }

        // 7. Chi tiết Group Order
        [HttpGet("groups/{groupId}")]
        public async Task<IActionResult> GetOrderGroupDetail(Guid groupId)
        {
            var group = await _orderService.GetOrderGroupDetailAsync(groupId);
            return SuccessResponse(group);
        }

        // 8. Hủy đơn
        [HttpPost("{orderId}/cancel")]
        public async Task<IActionResult> CancelOrder(Guid orderId, [FromBody] CancelOrderRequest request)
        {
            var userId = GetCurrentUserId();
            await _orderService.CancelOrderAsync(userId, orderId, request.Reason);
            return SuccessResponse("Order cancelled successfully");
        }

        // --- Helper: Get User ID ---
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