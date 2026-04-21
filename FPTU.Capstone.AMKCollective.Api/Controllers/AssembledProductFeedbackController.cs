using FPTU.Capstone.AMKCollective.Api.Controllers;
using FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProductFeedback;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPTU.Capstone.AMKCollective.API.Controllers
{
    [Route("api/v1")]
    [ApiController]
    public class AssembledProductFeedbackController : BaseApiController
    {
        private readonly IAssembledProductFeedbackService _feedbackService;

        public AssembledProductFeedbackController(IAssembledProductFeedbackService feedbackService)
        {
            _feedbackService = feedbackService;
        }

        /// <summary>
        /// Khach hang tao danh gia cho assembled product theo order item.
        /// </summary>
        /// <remarks>
        /// - API nay su dung [FromForm] (Form-Data) de ho tro upload file.
        /// - Key cho hinh anh la Images (cho phep chon nhieu file, toi da 5 file).
        /// </remarks>
        [HttpPost("order-items/{orderItemId}/assembled-feedbacks")]
        [Authorize]
        public async Task<IActionResult> CreateAssembledFeedback(Guid orderItemId, [FromForm] CreateAssembledProductFeedbackRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _feedbackService.CreateAsync(userId, orderItemId, request);
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
        /// Khach hang cap nhat danh gia assembled product (chi duoc chinh 1 lan).
        /// </summary>
        [HttpPut("assembled-feedbacks/{feedbackId}")]
        [Authorize]
        public async Task<IActionResult> UpdateAssembledFeedback(Guid feedbackId, [FromForm] UpdateAssembledProductFeedbackRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _feedbackService.UpdateAsync(userId, feedbackId, request);
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
        /// Chu Shop phan hoi lai danh gia assembled product.
        /// </summary>
        [HttpPut("assembled-feedbacks/{feedbackId}/reply")]
        [Authorize]
        public async Task<IActionResult> ReplyAssembledFeedback(Guid feedbackId, [FromBody] ReplyAssembledProductFeedbackRequest request)
        {
            try
            {
                var shopUserId = GetCurrentUserId();
                var result = await _feedbackService.ReplyAsync(shopUserId, feedbackId, request);
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
        /// Hien thi danh sach danh gia cua assembled product.
        /// </summary>
        [HttpGet("assembled-products/{productId}/feedbacks")]
        public async Task<IActionResult> GetAssembledProductFeedbacks(Guid productId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _feedbackService.GetByProductIdAsync(productId, pageNumber, pageSize);
                return SuccessResponse(result, "Fetched feedbacks successfully.");
            }
            catch (Exception ex)
            {
                return ErrorResponse(ex.Message);
            }
        }

        /// <summary>
        /// Lay thong tin eligibility feedback cho assembled product.
        /// </summary>
        [HttpGet("assembled-products/{productId}/feedback-eligibility")]
        [Authorize]
        public async Task<IActionResult> GetAssembledProductFeedbackEligibility(Guid productId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _feedbackService.GetEligibilityByProductIdAsync(userId, productId);
                return SuccessResponse(result, "Fetched feedback eligibility successfully.");
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
        /// Kenh nguoi ban: Chu shop lay danh sach danh gia assembled product cua shop.
        /// </summary>
        [HttpGet("assembled-feedbacks/my-shop")]
        [Authorize]
        public async Task<IActionResult> GetMyShopAssembledFeedbacks([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
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

        /// <summary>
        /// Khach hang xem lai chi tiet danh gia cua chinh minh cho mot order item.
        /// </summary>
        [HttpGet("order-items/{orderItemId}/my-assembled-feedback")]
        [Authorize]
        public async Task<IActionResult> GetMyFeedbackByItem(Guid orderItemId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var result = await _feedbackService.GetMyFeedbackByItemAsync(userId, orderItemId);

                if (result == null)
                {
                    return SuccessResponse<AssembledProductFeedbackResponse?>(null, "Feedback not found.");
                }

                return SuccessResponse(result, "Fetched my feedback successfully.");
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
