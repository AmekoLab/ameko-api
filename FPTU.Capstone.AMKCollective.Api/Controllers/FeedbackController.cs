using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs.Feedback;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    [Route("api/v1")]
    [ApiController]
    public class FeedbackController : BaseApiController
    {
        private readonly IFeedbackService _feedbackService;

        public FeedbackController(IFeedbackService feedbackService)
        {
            _feedbackService = feedbackService;
        }

        /// <summary>
        /// Khách hàng tạo đánh giá cho một đơn hàng đã hoàn thành.
        /// </summary>
        /// <remarks>
        /// - API này sử dụng `[FromForm]` (Form-Data) thay vì JSON để hỗ trợ upload file.
        /// - Key cho hình ảnh là `Images` (cho phép chọn nhiều file, tối đa 5 file).
        /// </remarks>
        /// <param name="orderId">Mã đơn hàng cần đánh giá</param>
        /// <param name="request">Payload chứa số sao (1-5), nội dung chữ và danh sách hình ảnh đính kèm</param>
        /// <returns>Dữ liệu đánh giá vừa được tạo, kèm theo danh sách URL hình ảnh đã upload</returns>
        [HttpPost("orders/{orderId}/feedbacks")]
        [Authorize]
        public async Task<IActionResult> CreateFeedback(Guid orderId, [FromForm] CreateFeedbackRequest request)
        {
            try
            {
                // Sử dụng hàm GetCurrentUserId() từ BaseApiController
                var userId = GetCurrentUserId();
                var result = await _feedbackService.CreateFeedbackAsync(userId, orderId, request);

                return SuccessResponse(result, "Feedback submitted successfully.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        /// <summary>
        /// Khach hang cap nhat danh gia (chi duoc chinh 1 lan).
        /// </summary>
        [HttpPut("feedbacks/{feedbackId}")]
        [Authorize]
        public async Task<IActionResult> UpdateFeedback(Guid feedbackId, [FromForm] UpdateFeedbackRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _feedbackService.UpdateFeedbackAsync(userId, feedbackId, request);

                return SuccessResponse(result, "Feedback updated successfully.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        /// <summary>
        /// Chủ Shop phản hồi lại một đánh giá của khách hàng.
        /// </summary>
        /// <remarks>
        /// - Mỗi đánh giá chỉ được phép phản hồi 1 lần duy nhất.
        /// - Sử dụng format JSON bình thường.
        /// </remarks>
        /// <param name="feedbackId">Mã ID của đánh giá (FeedbackId) cần phản hồi</param>
        /// <param name="request">Payload chứa nội dung phản hồi của shop</param>
        /// <returns>Dữ liệu đánh giá đã được cập nhật thêm nội dung phản hồi từ Shop</returns>
        [HttpPut("feedbacks/{feedbackId}/reply")]
        [Authorize]
        public async Task<IActionResult> ReplyFeedback(Guid feedbackId, [FromBody] ReplyFeedbackRequest request)
        {
            try
            {
                // Lấy User Id của chủ shop đang đăng nhập
                var shopUserId = GetCurrentUserId();
                var result = await _feedbackService.ReplyFeedbackAsync(shopUserId, feedbackId, request);

                return SuccessResponse(result, "Replied to feedback successfully.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        /// <summary>
        /// Hiển thị toàn bộ danh sách đánh giá của một Shop cụ thể.
        /// </summary>
        /// <remarks>
        /// - API Public: Ai cũng có thể xem, không cần gửi Token.
        /// - Thường dùng để hiển thị trên trang Profile của Shop hoặc trang xem chi tiết sản phẩm thuộc shop đó.
        /// - Trả về danh sách chứa thông tin người đánh giá, số sao, hình ảnh upload, và cả lời phản hồi của shop (nếu có).
        /// </remarks>
        /// <param name="shopId">Mã ID của Shop cần lấy danh sách đánh giá</param>
        /// <returns>Danh sách các đánh giá của Shop, sắp xếp từ mới nhất đến cũ nhất</returns>
        [HttpGet("shops/{shopId}/feedbacks")]
        public async Task<IActionResult> GetShopFeedbacks(Guid shopId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _feedbackService.GetShopFeedbacksAsync(shopId, pageNumber, pageSize);
                return SuccessResponse(result, "Fetched feedbacks successfully.");
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }
        /// <summary>
        /// Kênh Người Bán: Chủ Shop lấy danh sách toàn bộ đánh giá của gian hàng mình.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <param name="pageNumber">Số thứ tự trang cần lấy (Mặc định: 1)</param>
        /// <param name="pageSize">Số lượng đánh giá hiển thị trên 1 trang (Mặc định: 10)</param>
        /// <returns>Danh sách đánh giá đã được phân trang (PaginatedResult) của gian hàng, sắp xếp mới nhất lên đầu.</returns>
        [HttpGet("feedbacks/my-shop")]
        [Authorize]
        public async Task<IActionResult> GetMyShopFeedbacks([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _feedbackService.GetMyShopFeedbacksAsync(userId, pageNumber, pageSize);

                return SuccessResponse(result, "Fetched my shop feedbacks successfully.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return UnauthorizedResponse<object>(ex.Message);
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }
    }
}
