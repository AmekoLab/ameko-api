using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using FPTU.Capstone.AMKCollective.Application.DTOs.Order;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
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
        [SwaggerOperation(
    Summary = "Process Checkout",
    Description = "Finalizes the order based on items in the cart or immediate purchase requests."
)]
        [SwaggerResponse(200, "Checkout initiated successfully")]
        [SwaggerResponse(401, "Unauthorized")]
        public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _orderService.CheckoutAsync(userId, request);
            return SuccessResponse(result, "Checkout initiated successfully");
        }

        // 2. Thêm vào giỏ
        [HttpPost("cart")]
        [SwaggerOperation(
    Summary = "Add Item to Cart",
    Description = "Adds a product or a custom built keyboard to the user's shopping cart."
)]
        [SwaggerResponse(200, "Item added to cart")]
        [SwaggerResponse(401, "Unauthorized")]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
        {
            var userId = GetCurrentUserId();
            await _orderService.AddToCartAsync(userId, request);
            return SuccessResponse("Item added to cart successfully");
        }

        // 3. Lấy giỏ hàng
        [HttpGet("cart")]
        [SwaggerOperation(
    Summary = "Get My Cart",
    Description = "Retrieves the current user's shopping cart details."
)]
        [SwaggerResponse(200, "Cart retrieved successfully")]
        [SwaggerResponse(401, "Unauthorized")]
        public async Task<IActionResult> GetMyCart()
        {
            var userId = GetCurrentUserId();
            var cart = await _orderService.GetMyCartAsync(userId);
            return SuccessResponse(cart, "Cart retrieved successfully");
        }

        // 4. Xóa item khỏi giỏ
        [HttpDelete("cart/{itemId}")]
        [SwaggerOperation(
    Summary = "Remove Item from Cart",
    Description = "Removes a specific item from the shopping cart."
)]
        [SwaggerResponse(200, "Item removed successfully")]
        [SwaggerResponse(401, "Unauthorized")]
        public async Task<IActionResult> RemoveFromCart(Guid itemId)
        {
            var userId = GetCurrentUserId();
            await _orderService.RemoveItemFromCartAsync(userId, itemId);

            return SuccessResponse("Item removed from cart");
        }

        // 5. Cập nhật số lượng
        [HttpPut("cart/{itemId}")]
        [SwaggerOperation(
    Summary = "Update Cart Item Quantity",
    Description = "Updates the quantity of a specific item in the cart."
)]
        [SwaggerResponse(200, "Cart updated successfully")]
        [SwaggerResponse(401, "Unauthorized")]
        public async Task<IActionResult> UpdateCartItem(Guid itemId, [FromBody] UpdateCartItemRequest request)
        {
            var userId = GetCurrentUserId();
            await _orderService.UpdateCartItemQuantityAsync(userId, itemId, request.Quantity);

            return SuccessResponse("Cart item updated successfully");
        }

        // 1. GET MY ORDERS (Flat list)
        [HttpGet("my-orders")]
        [SwaggerOperation(
            Summary = "Get detailed list of orders",
            Description = "Returns a flat list of individual orders for tracking shipment and shop-level status."
        )]
        [SwaggerResponse(200, "Success", typeof(List<OrderResponse>))]
        [SwaggerResponse(401, "Unauthorized")]
        public async Task<IActionResult> GetMyOrders()
        {
            var userId = GetCurrentUserId();
            var orders = await _orderService.GetMyOrdersAsync(userId);

            return SuccessResponse(orders, "Order list retrieved successfully.");
        }

        // 2. GET PAYMENT HISTORY (Grouped)
        [HttpGet("my-payment-history")]
        [SwaggerOperation(
            Summary = "Get payment history (Grouped)",
            Description = "Returns a list of payment batches (Order Groups). Each group contains multiple child orders."
        )]
        [SwaggerResponse(200, "Success", typeof(List<OrderGroupResponse>))]
        [SwaggerResponse(401, "Unauthorized")]
        public async Task<IActionResult> GetMyPaymentHistory()
        {
            var userId = GetCurrentUserId();

            var orderGroups = await _orderService.GetMyOrderGroupsAsync(userId);

            return SuccessResponse(orderGroups, "Payment history retrieved successfully.");
        }




        // 7. Chi tiết Group Order
        [HttpGet("groups/{groupId}")]
        [SwaggerOperation(
    Summary = "Get Order Group Detail",
    Description = "Retrieves details of a group order (for Group Buy)."
)]
        [SwaggerResponse(200, "Group details retrieved")]
        [SwaggerResponse(404, "Group not found")]
        public async Task<IActionResult> GetOrderGroupDetail(Guid groupId)
        {
            var group = await _orderService.GetOrderGroupDetailAsync(groupId);
            return SuccessResponse(group);
        }

        // 8. Hủy đơn
        [HttpPost("{orderId}/cancel")]
        [SwaggerOperation(
    Summary = "Cancel Order",
    Description = "Cancels a pending order if it meets the cancellation criteria."
)]
        [SwaggerResponse(200, "Order cancelled successfully")]
        [SwaggerResponse(401, "Unauthorized")]
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