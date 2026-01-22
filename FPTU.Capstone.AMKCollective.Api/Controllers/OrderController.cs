using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs;
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

        // ======================= CUSTOMER (KHÁCH HÀNG) =======================

        /// <summary>
        /// Tạo đơn hàng mới và lấy link thanh toán (Checkout)
        /// </summary>
        /// <param name="request">Thông tin giỏ hàng và địa chỉ giao hàng</param>
        /// <returns>Link thanh toán Stripe và thông tin đơn hàng tổng</returns>
        [HttpPost("checkout")]
        [Authorize] // Yêu cầu đăng nhập
        public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request)
        {
            try
            {
                //TODO: waiting for auth
                var userId = GetUserId();
                
                var result = await _orderService.CheckoutAsync(userId, request);
                return SuccessResponse(result, "Order create successfully. Please proceed with payment.");
            }
            catch (Exception ex)
            {
                // Log ex here
                return ErrorResponse<string>(ex.Message);
            }
        }

        /// <summary>
        /// Lấy danh sách lịch sử mua hàng của tôi
        /// </summary>
        /// <returns>Danh sách các nhóm đơn hàng</returns>
        [HttpGet("my-orders")]
        [Authorize]
        public async Task<IActionResult> GetMyOrders()
        {
            //TODO: waiting for auth
            var userId = GetUserId();
            
            var orders = await _orderService.GetMyOrdersAsync(userId);
            return SuccessResponse(orders);
        }

        /// <summary>
        /// Xem chi tiết một nhóm đơn hàng (Order Group)
        /// </summary>
        /// <param name="id">Order Group ID</param>
        /// <returns>Chi tiết đơn hàng</returns>
        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetOrderGroupDetail(Guid id)
        {
            try
            {
                var order = await _orderService.GetOrderGroupDetailAsync(id);
                return SuccessResponse(order);
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse<string>("Order not found");
            }
        }

        // ======================= SHOP OWNER (CHỦ SHOP) =======================

        /// <summary>
        /// (Dành cho Shop) Lấy danh sách đơn hàng của Shop
        /// </summary>
        /// <param name="status">Lọc theo trạng thái (Pending, Shipping, Completed...)</param>
        /// <param name="page">Số trang (mặc định 1)</param>
        /// <param name="size">Số lượng item/trang (mặc định 10)</param>
        /// <returns>Danh sách đơn hàng thuộc về Shop</returns>
        [HttpGet("shop/orders")]
        [Authorize(Roles = "ShopOwner")] // Giả sử bạn có Role này
        public async Task<IActionResult> GetShopOrders([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int size = 10)
        {
            // Giả định ShopId được lấy từ Token hoặc Claims của User đang login
            // Logic lấy ShopId tùy thuộc vào hệ thống Auth của bạn. 
            // Ở đây mình ví dụ lấy từ Claim "ShopId"
            var shopIdClaim = User.FindFirst("ShopId")?.Value;
            if (string.IsNullOrEmpty(shopIdClaim))
            {
                return ErrorResponse<string>("user does not have permission");
            }

            var shopId = Guid.Parse(shopIdClaim);
            var orders = await _orderService.GetShopOrdersAsync(shopId, status, page, size);

            return SuccessResponse(orders);
        }

        /// <summary>
        /// (Dành cho Shop) Cập nhật trạng thái đơn hàng
        /// </summary>
        /// <param name="id">Order ID (Đơn con)</param>
        /// <param name="newStatus">Trạng thái mới (VD: Shipping, Completed)</param>
        /// <returns>Thông báo thành công</returns>
        [HttpPut("shop/orders/{id}/status")]
        [Authorize(Roles = "ShopOwner")]
        public async Task<IActionResult> UpdateOrderStatus(Guid id, [FromBody] string newStatus)
        {
            try
            {
                var shopIdClaim = User.FindFirst("ShopId")?.Value;
                if (string.IsNullOrEmpty(shopIdClaim)) return ErrorResponse<string>("Unauthorized");
                var shopId = Guid.Parse(shopIdClaim);

                await _orderService.UpdateOrderStatusAsync(shopId, id, newStatus);
                return SuccessResponse("Cập nhật trạng thái đơn hàng thành công");
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse<string>("Không tìm thấy đơn hàng hoặc bạn không có quyền.");
            }
        }

        private Guid GetUserId()
        {
            var idClaim = User.FindFirst("id") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (idClaim == null) throw new UnauthorizedAccessException("Invalid Token");
            return Guid.Parse(idClaim.Value);
        }
    }
}