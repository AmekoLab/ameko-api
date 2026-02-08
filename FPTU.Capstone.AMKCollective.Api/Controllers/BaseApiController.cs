using Microsoft.AspNetCore.Mvc;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using System.Security.Claims;

namespace FPTU.Capstone.AMKCollective.Api.Controllers
{
    /// <summary>
    /// Base Controller with standard response methods
    /// </summary>
    [ApiController]
    public class BaseApiController : ControllerBase
    {
        /// <summary>
        /// Returns a successful response with data
        /// </summary>
        protected IActionResult SuccessResponse<T>(T data, string message = "Success")
        {
            return Ok(ApiResponse<T>.SuccessResponse(data, message));
        }

        /// <summary>
        /// Returns a successful response without data
        /// </summary>
        protected IActionResult SuccessResponse(string message = "Success")
        {
            return Ok(ApiResponse<object>.SuccessResponse(message));
        }

        /// <summary>
        /// Returns a bad request error response
        /// </summary>
        protected IActionResult ErrorResponse<T>(string message, List<string>? errors = null)
        {
            return BadRequest(ApiResponse<T>.ErrorResponse(message, errors));
        }

        /// <summary>
        /// Returns a not found error response
        /// </summary>
        protected IActionResult NotFoundResponse<T>(string message = "Resource not found")
        {
            return NotFound(ApiResponse<T>.ErrorResponse(message));
        }

        /// <summary>
        /// Returns a server error response
        /// </summary>
        protected IActionResult ServerErrorResponse<T>(string message = "Internal server error")
        {
            return StatusCode(500, ApiResponse<T>.ErrorResponse(message));
        }

        /// <summary>
        /// Returns an unauthorized error response
        /// </summary>
        protected IActionResult UnauthorizedResponse<T>(string message = "Unauthorized")
        {
            return Unauthorized(ApiResponse<T>.ErrorResponse(message));
        }

        /// <summary>
        /// Gets the current user's ID from the JWT token
        /// </summary>
        /// <returns>ID of the current user</returns>
        /// <exception cref="UnauthorizedAccessException">User ID not found in token</exception>
        protected Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub") ?? User.FindFirst("id");
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out Guid userId))
            {
                return userId;
            }
            throw new UnauthorizedAccessException("User ID not found in token");
        }
    }
}
