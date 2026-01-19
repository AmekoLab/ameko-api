using FPTU.Capstone.AMKCollective.Application.DTOs.Auth;
using FPTU.Capstone.AMKCollective.Application.DTOs.User;
using FPTU.Capstone.AMKCollective.Application.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using FPTU.Capstone.AMKCollective.Application.DTOs.Auth;
using FPTU.Capstone.AMKCollective.Application.DTOs.User;


namespace FPTU.Capstone.AMKCollective.Api.Controllers
{
    [ApiController]

    [Route("api/[controller]")]
    public class UsersController : BaseApiController

    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var users = _userService.GetAll();
            return SuccessResponse(users);
        }

        /// <summary>
        /// Authenticate user and return token
        /// </summary>
        /// <param name="request">Login credentials</param>
        /// <returns>Login response with token and user info</returns>
       
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            var response = _userService.Login(request);
            if (response == null) return UnauthorizedResponse<LoginResponse>("Invalid username or password");
            return SuccessResponse(response);
        }

        /// <summary>
        /// Get user profile by Id
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>User profile details</returns>
        [Authorize]
        [HttpGet("profile/{id}")]
        public IActionResult GetProfile(Guid id)
        {
            var profile = _userService.GetProfile(id);
            if (profile == null) return NotFoundResponse<UserProfileDto>("User not found");
            return SuccessResponse(profile);
        }

        /// <summary>
        /// Register a new customer account
        /// </summary>
        /// <param name="request">Registration details</param>
        /// <returns>Status of registration</returns>
        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterRequest request)
        {
            if (!_userService.Register(request, out string errorMessage))
            {
                return ErrorResponse<object>(errorMessage);
            }
            return SuccessResponse(new 
            { 
                request.Username, 
                request.Email, 
                request.FirstName, 
                request.LastName,
                request.Role
            }, "Registration successful");
        }

        /// <summary>
        /// Change password for the current user
        /// </summary>
        /// <param name="userId">User ID (from token in real app)</param>
        /// <param name="request">Old and new password</param>
        /// <returns>Status</returns>
        [HttpPost("change-password/{userId}")] 
        public IActionResult ChangePassword(Guid userId, [FromBody] ChangePasswordRequest request)
        {
             if (!_userService.ChangePassword(userId, request, out string errorMessage))
            {
                return ErrorResponse<object>(errorMessage);
            }
            return SuccessResponse(new { UserId = userId }, "Password changed successfully");
        }

        /// <summary>
        /// Logout and revoke refresh token
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="request">Refresh token to revoke</param>
        /// <returns>Status</returns>
        [HttpPost("logout/{userId}")]
        public IActionResult Logout(Guid userId, [FromBody] LogoutRequest request)
        {
             if (!_userService.Logout(userId, request, out string errorMessage))
            {
                return ErrorResponse<object>(errorMessage);
            }
            return SuccessResponse("Logout successful");
        }

        /// <summary>
        /// Refresh access token using refresh token
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="request">Refresh token</param>
        /// <returns>New access and refresh token</returns>
        [HttpPost("refresh-token/{userId}")]
        public IActionResult RefreshToken(Guid userId, [FromBody] RefreshTokenRequest request)
        {
            var response = _userService.RefreshToken(userId, request, out string errorMessage);
            if (response == null)
            {
                return ErrorResponse<object>(errorMessage);
            }
            return SuccessResponse(response, "Token refreshed successfully");
        }

        #region Admin Endpoints

        /// <summary>
        /// Admin: Create a new user (any role)
        /// </summary>
        /// <param name="request">User details</param>
        /// <returns>Created User ID</returns>
        [HttpPost("admin/users")]
        public IActionResult CreateUser([FromBody] CreateUserRequest request)
        {
             var userId = _userService.CreateUser(request, out string errorMessage);
             if (userId == Guid.Empty)
             {
                 return ErrorResponse<object>(errorMessage);
             }
             return SuccessResponse(new 
             { 
                 UserId = userId,
                 request.Username, 
                 request.Email, 
                 request.FirstName, 
                 request.LastName,
                 request.Role,
                 request.Status,
                 request.Gender,
                 request.DateOfBirth,
                 request.PhoneNumber
                 // Exclude Password
             }, "User created successfully");
        }

        /// <summary>
        /// Admin: Update a user
        /// </summary>
        /// <param name="id">User ID</param>
        /// <param name="request">Update details</param>
        /// <returns>Status</returns>
        [HttpPut("admin/users/{id}")]
        public IActionResult AdminUpdateUser(Guid id, [FromBody] UpdateUserAdminRequest request)
        {
            if (!_userService.AdminUpdateUser(id, request, out string errorMessage))
            {
                return ErrorResponse<object>(errorMessage);
            }
            return SuccessResponse(request, "User updated successfully");
        }

        /// <summary>
        /// Admin: Delete a user
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Status</returns>
        [HttpDelete("admin/users/{id}")]
        public IActionResult DeleteUser(Guid id)
        {
            if (!_userService.DeleteUser(id, out string errorMessage))
            {
                return ErrorResponse<object>(errorMessage);
            }
            return SuccessResponse(new { UserId = id }, "User deleted successfully");
        }

        /// <summary>
        /// Admin: Ban a user (Set status to Suspended)
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Status</returns>
        [HttpPost("admin/users/{id}/ban")]
        public IActionResult BanUser(Guid id)
        {
            if (!_userService.BanUser(id, out string errorMessage))
            {
                return ErrorResponse<object>(errorMessage);
            }
            return SuccessResponse(new { UserId = id }, "User banned successfully");
        }

        #endregion

        /// <summary>
        /// Update user profile
        /// </summary>
        /// <param name="id">User ID</param>
        /// <param name="request">Update data</param>
        /// <returns>Status of update</returns>
        [HttpPut("profile/{id}")]
        public IActionResult UpdateProfile(Guid id, [FromBody] UpdateProfileRequest request)
        {
            var result = _userService.UpdateProfile(id, request);
            if (!result) return NotFoundResponse<object>("User not found or update failed");
            return SuccessResponse(request, "Profile updated successfully");
        }



        /// <summary>
        /// Request password reset code via email
        /// </summary>
        /// <param name="request">Email</param>
        /// <returns>Status</returns>
        [HttpPost("forgot-password")]
        public IActionResult ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (!_userService.ForgotPassword(request, out string errorMessage))
            {
                return BadRequest(new { Message = errorMessage });
            }
            return Ok(new { Message = "Password reset code sent to email" });
        }

        /// <summary>
        /// Reset password using code
        /// </summary>
        /// <param name="request">Email, Code, NewPassword</param>
        /// <returns>Status</returns>
        [HttpPost("reset-password")]
        public IActionResult ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (!_userService.ResetPassword(request, out string errorMessage))
            {
                return BadRequest(new { Message = errorMessage });
            }
            return Ok(new { Message = "Password reset successfully" });
        }





   
    }
}
