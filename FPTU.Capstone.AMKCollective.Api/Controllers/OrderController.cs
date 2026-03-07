using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using FPTU.Capstone.AMKCollective.Application.DTOs.Order;
using FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Application.Services;
using FPTU.Capstone.AMKCollective.Domain.Enums;
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
        private readonly IShopService _shopService;

        public OrderController(IOrderService orderService, IShopService shopService)
        {
            _orderService = orderService;
            _shopService = shopService;
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
        [Obsolete("This API is deprecated and disabled. Please use /orders/cancel/v2 instead.")]
        [HttpPost("{orderId}/cancel")]
        [SwaggerOperation(
    Summary = "Cancel Order",
    Description = "Cancels a pending order if it meets the cancellation criteria."
)]
        [SwaggerResponse(200, "Order cancelled successfully")]
        [SwaggerResponse(401, "Unauthorized")]
        public async Task<IActionResult> CancelOrder(Guid orderId, [FromBody] Application.DTOs.OrderIssues.CancelOrderRequest request)
        {
            var userId = GetCurrentUserId();
            await _orderService.CancelOrderAsync(userId, orderId, request.Reason);
            return SuccessResponse("Order cancelled successfully");
        }


        /// <summary>
        /// [Customer] Submit an order cancellation request (instead of immediate cancellation)
        /// </summary>
        [HttpPost("cancel-request")]
        [Authorize]
        public async Task<IActionResult> RequestCancelOrder([FromBody] Application.DTOs.OrderIssues.CancelOrderRequest request)
        {
            var userId = GetCurrentUserId(); // Get ID from Token

            var result = await _orderService.RequestCancelOrderAsync(userId, request);

            if (result.Status == Domain.Enums.OrderIssueStatus.Rejected)
            {
                // System auto-reject (due to Spam limit or Order already shipped)
                // Using ErrorResponse from BaseApiController
                return ErrorResponse<object>($"Cancellation request rejected. {result.ShopResponse}");
            }

            // Using SuccessResponse from BaseApiController
            return SuccessResponse(result, "Request submitted successfully. Please wait for Shop approval.");
        }

        /// <summary>
        /// [Shop Owner] Approve or Reject customer's cancellation request
        /// </summary>
        [HttpPost("process-issue")]
        [Authorize]
        public async Task<IActionResult> ProcessCancelRequest([FromBody] ProcessIssueRequest request)
        {
            var userId = GetCurrentUserId();

            // 1. Get Shop info of the current logged-in User
            // (Assuming ShopService has GetShopByUserIdAsync)
            var shop = await _shopService.GetMyShopAsync(userId);

            if (shop == null)
            {
                return ErrorResponse<object>("This account is not a Shop Owner.");
            }

            // 2. Call Service to process
            await _orderService.ProcessCancelRequestAsync(userId, request);

            return SuccessResponse(true, "Request processed successfully.");
        }

        /// <summary>
        /// Create a new payment session for an existing Pending order (Repay)
        /// </summary>
        /// <param name="orderGroupId">The ID of the order group to pay</param>
        /// <returns>Payment URL</returns>
        [HttpPost("repay/{orderGroupId}")]
        public async Task<IActionResult> Repay(Guid orderGroupId)
        {
            try
            {
                var userId = GetCurrentUserId();

                // 2. Gọi Service để lấy link thanh toán mới
                var paymentUrl = await _orderService.RepayAsync(userId, orderGroupId);

                // 3. Trả về link thanh toán (Dùng hàm SuccessResponse của BaseApiController)
                return SuccessResponse(new { PaymentUrl = paymentUrl }, "Payment link generated successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                // Trả về 404 Not Found
                return NotFoundResponse<string>(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                // Trả về 401 Unauthorized
                return UnauthorizedResponse<string>(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                // Trả về 400 Bad Request (Ví dụ: Đơn đã thanh toán rồi, hoặc đã hủy)
                return ErrorResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                // Trả về 500 Internal Server Error
                return ServerErrorResponse<string>(ex.Message);
            }
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrderDetail(Guid id)
        {
            try
            {
                // 1. Lấy UserId từ Token
                var userId = GetCurrentUserId();

                // 2. Gọi Service
                var orderDetail = await _orderService.GetOrderDetailAsync(userId, id);

                // 3. Trả về kết quả
                return SuccessResponse(orderDetail, "Order details retrieved successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<string>(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<string>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<string>(ex.Message);
            }
        }

        /// <summary>
        /// [Shop Owner] Lấy danh sách đơn hàng của Shop (Có phân trang và lọc theo trạng thái)
        /// </summary>
        [HttpGet("shop")]
        [Authorize]
        [SwaggerOperation(Summary = "[Shop] Get shop orders", Description = "Retrieves a paginated list of orders for the logged-in shop owner.")]
        public async Task<IActionResult> GetShopOrders([FromQuery] OrderStatus? status, [FromQuery] int page = 1, [FromQuery] int size = 10)
        {
            try
            {
                var userId = GetCurrentUserId();
                var shop = await _shopService.GetMyShopAsync(userId);

                if (shop == null)
                    return ErrorResponse<object>("This account does not own a shop.");

                var orders = await _orderService.GetShopOrdersAsync(shop.Id, status, page, size);
                return SuccessResponse(orders, "Shop orders retrieved successfully.");
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// [Shop Owner] Xem chi tiết một đơn hàng cụ thể của Shop
        /// </summary>
        [HttpGet("shop/{orderId}")]
        [Authorize]
        [SwaggerOperation(Summary = "[Shop] Get shop order details", Description = "Retrieves details of a specific order for the shop.")]
        public async Task<IActionResult> GetShopOrderDetail(Guid orderId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var shop = await _shopService.GetMyShopAsync(userId);

                if (shop == null)
                    return ErrorResponse<object>("This account does not own a shop.");

                var order = await _orderService.GetShopOrderDetailAsync(shop.Id, orderId);
                return SuccessResponse(order, "Order details retrieved successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }

        /// <summary>
        /// [Shop Owner] Cập nhật trạng thái đơn hàng (Ví dụ: Từ Pending sang Processing, Shipped...)
        /// </summary>
        [HttpPut("shop/{orderId}/status")]
        [Authorize]
        [SwaggerOperation(Summary = "[Shop] Update order status", Description = "Updates the status of an order.")]
        public async Task<IActionResult> UpdateOrderStatus(Guid orderId, [FromBody] OrderStatus newStatus)
        {
            try
            {
                var userId = GetCurrentUserId();
                var shop = await _shopService.GetMyShopAsync(userId);

                if (shop == null)
                    return ErrorResponse<object>("This account does not own a shop.");

                await _orderService.UpdateOrderStatusAsync(shop.Id, orderId, newStatus);
                return SuccessResponse("Order status updated successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse<object>(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ServerErrorResponse<object>(ex.Message);
            }
        }
        // --- Helper: Get User ID ---
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