using Microsoft.AspNetCore.Mvc;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;

namespace FPTU.Capstone.AMKCollective.Api.Controllers
{
    /// <summary>
    /// Base Controller với các phương thức trả response chuẩn
    /// </summary>
    [ApiController]
    public class BaseApiController : ControllerBase
    {
        /// <summary>
        /// Trả về response thành công với data
        /// </summary>
        protected IActionResult SuccessResponse<T>(T data, string message = "Success")
        {
            return Ok(ApiResponse<T>.SuccessResponse(data, message));
        }

        /// <summary>
        /// Trả về response thành công không có data
        /// </summary>
        protected IActionResult SuccessResponse(string message = "Success")
        {
            return Ok(ApiResponse<object>.SuccessResponse(message));
        }

        /// <summary>
        /// Trả về response lỗi bad request
        /// </summary>
        protected IActionResult ErrorResponse<T>(string message, List<string>? errors = null)
        {
            return BadRequest(ApiResponse<T>.ErrorResponse(message, errors));
        }

        /// <summary>
        /// Trả về response lỗi not found
        /// </summary>
        protected IActionResult NotFoundResponse<T>(string message = "Resource not found")
        {
            return NotFound(ApiResponse<T>.ErrorResponse(message));
        }

        /// <summary>
        /// Trả về response lỗi server
        /// </summary>
        protected IActionResult ServerErrorResponse<T>(string message = "Internal server error")
        {
            return StatusCode(500, ApiResponse<T>.ErrorResponse(message));
        }

        /// <summary>
        /// Trả về response lỗi unauthorized
        /// </summary>
        protected IActionResult UnauthorizedResponse<T>(string message = "Unauthorized")
        {
            return Unauthorized(ApiResponse<T>.ErrorResponse(message));
        }
    }
}
