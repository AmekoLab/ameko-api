using FPTU.Capstone.AMKCollective.Application.DTOs.Auth;
using FPTU.Capstone.AMKCollective.Application.DTOs.User;
using FPTU.Capstone.AMKCollective.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;


namespace FPTU.Capstone.AMKCollective.Api.Controllers
{
    [ApiController]

    [Route("api/v1/[controller]")]
    public class UsersController : BaseApiController

    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }
        [SwaggerOperation(
            Summary = "Get all users",
            Description = "Returns a list of all registered users. Admin role suggested for production." )]
        [SwaggerResponse(200, "Successfully retrieved list of users", typeof(IEnumerable<UserProfileDto>))]
        [SwaggerResponse(401, "Unauthorized access")]
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var users = await _userService.GetAllAsync();
            return SuccessResponse(users);
        }


        [HttpPost("login")]
        [SwaggerOperation(
            Summary = "Authenticate user",
            Description = "Authenticate user credentials and return access token, refresh token, and user information"
        )]
        [SwaggerResponse(200, "Login successful", typeof(LoginResponse))]
        [SwaggerResponse(401, "Invalid username or password")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var response = await _userService.LoginAsync(request);
            if (response == null)
                return UnauthorizedResponse<LoginResponse>("Invalid username or password");

            return SuccessResponse(response);
        }


        #region Profile

        [Authorize]
        [HttpGet("profile/{id}")]
        [SwaggerOperation(
            Summary = "Get user profile",
            Description = "Retrieve detailed profile information of a user by UserId"
        )]
        [SwaggerResponse(200, "Success", typeof(UserProfileDto))]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(404, "User not found")]
        public async Task<IActionResult> GetProfile(Guid id)
        {
            var profile = await _userService.GetProfileAsync(id);
            if (profile == null)
                return NotFoundResponse<UserProfileDto>("User not found");

            return SuccessResponse(profile);
        }

        #endregion

        #region Registration

        [HttpPost("register")]
        [SwaggerOperation(
            Summary = "Register new customer",
            Description = "Create a new customer account with default role 'Customer'."
        )]
        [SwaggerResponse(200, "Registration successful")]
        [SwaggerResponse(400, "Registration failed due to existing username/email or validation error")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var result = await _userService.RegisterAsync(request);
            if (!result.Success)
                return ErrorResponse<object>(result.ErrorMessage);

            return SuccessResponse(new
            {
                request.Username,
                request.Email,
                request.FirstName,
                request.LastName,
                request.Role
            }, "Registration successful");
        }

        #endregion

        #region Password Management

        [Authorize]
        [HttpPost("change-password/{userId}")]
        [SwaggerOperation(
            Summary = "Change password",
            Description = "Updates the authenticated user's password. Requires the old password for verification. Invalidates all active sessions (Refresh Tokens) upon success for security."
        )]
        [SwaggerResponse(200, "Password changed successfully")]
        [SwaggerResponse(400, "Incorrect old password or invalid new password format")]
        [SwaggerResponse(401, "Unauthorized - Bearer token required")]
        public async Task<IActionResult> ChangePassword(Guid userId, [FromBody] ChangePasswordRequest request)
        {
            var result = await _userService.ChangePasswordAsync(userId, request);
            if (!result.Success)
                return ErrorResponse<object>(result.ErrorMessage);

            return SuccessResponse(new { UserId = userId }, "Password changed successfully");
        }

        #endregion

        #region Logout & Token

        [Authorize]
        [HttpPost("logout/{userId}")]
        [SwaggerOperation(
            Summary = "Logout",
            Description = "Revokes the provided refresh token to terminate the current session. The user remains logged in on other devices."
        )]
        [SwaggerResponse(200, "Logged out successfully")]
        [SwaggerResponse(400, "Invalid or missing refresh token")]
        [SwaggerResponse(401, "Unauthorized - Bearer token required")]
        public async Task<IActionResult> Logout(Guid userId, [FromBody] LogoutRequest request)
        {
            var result = await _userService.LogoutAsync(userId, request);
            if (!result.Success)
                return ErrorResponse<object>(result.ErrorMessage);

            return SuccessResponse("Logout successful");
        }

        [HttpPost("refresh-token/{userId}")]
        [SwaggerOperation(
            Summary = "Refresh access token",
            Description = "Generate a new access token and rotating refresh token using a valid, non-expired refresh token."
        )]
        [SwaggerResponse(200, "Token refreshed successfully", typeof(RefreshTokenResponse))]
        [SwaggerResponse(400, "Invalid or expired refresh token")]
        public async Task<IActionResult> RefreshToken(Guid userId, [FromBody] RefreshTokenRequest request)
        {
            var result = await _userService.RefreshTokenAsync(userId, request);
            if (result.Response == null)
                return ErrorResponse<object>(result.ErrorMessage);

            return SuccessResponse(result.Response, "Token refreshed successfully");
        }

        #endregion

        #region Admin Endpoints

        [Authorize(Roles = "Admin")]
        [HttpPost("admin/create-user")]
        [SwaggerOperation(
            Summary = "Admin: Create user",
            Description = "Create a new confirmed user with a specific role, status, and profile information. Requires Admin role."
        )]
        [SwaggerResponse(200, "User created successfully")]
        [SwaggerResponse(400, "Failed to create user (e.g. username already exists)")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - Requires Admin role")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            var result = await _userService.CreateUserAsync(request);
            if (result.UserId == Guid.Empty)
                return ErrorResponse<object>(result.ErrorMessage);

            return SuccessResponse(new
            {
                result.UserId,
                request.Username,
                request.Email,
                request.FirstName,
                request.LastName,
                request.Role,
                request.Status,
                request.Gender,
                request.DateOfBirth,
                request.PhoneNumber
            }, "User created successfully");
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("admin/update-user/{id}")]
        [SwaggerOperation(
            Summary = "Admin: Update user",
            Description = "Fully update user information including role and account status. Requires Admin role."
        )]
        [SwaggerResponse(200, "User updated successfully")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - Requires Admin role")]
        [SwaggerResponse(404, "User not found")]
        public async Task<IActionResult> AdminUpdateUser(Guid id, [FromBody] UpdateUserAdminRequest request)
        {
            var result = await _userService.AdminUpdateUserAsync(id, request);
            if (!result.Success)
                return ErrorResponse<object>(result.ErrorMessage);

            return SuccessResponse(request, "User updated successfully");
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("admi/delete-user/{id}")]
        [SwaggerOperation(
            Summary = "Admin: Delete user",
            Description = "Permanently removes a user record from the system. Requires Admin role."
        )]
        [SwaggerResponse(200, "User deleted successfully")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - Requires Admin role")]
        [SwaggerResponse(404, "User not found")]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            var result = await _userService.DeleteUserAsync(id);
            if (!result.Success)
                return ErrorResponse<object>(result.ErrorMessage);

            return SuccessResponse(new { UserId = id }, "User deleted successfully");
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("admin/ban/{id}")]
        [SwaggerOperation(
            Summary = "Admin: Ban user",
            Description = "Suspends a user account, preventing further logins and invalidating all current sessions. Requires Admin role."
        )]
        [SwaggerResponse(200, "User banned successfully")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - Requires Admin role")]
        [SwaggerResponse(404, "User not found")]
        public async Task<IActionResult> BanUser(Guid id)
        {
            var result = await _userService.BanUserAsync(id);
            if (!result.Success)
                return ErrorResponse<object>(result.ErrorMessage);

            return SuccessResponse(new { UserId = id }, "User banned successfully");
        }

        #endregion

        #region Profile Update

        [Authorize]
        [HttpPut("profile/{id}")]
        [SwaggerOperation(
            Summary = "Update user profile",
            Description = "Updates personal details (First Name, Last Name, Gender, etc.) for the authenticated user."
        )]
        [SwaggerResponse(200, "Profile updated successfully")]
        [SwaggerResponse(401, "Unauthorized - Bearer token required")]
        [SwaggerResponse(404, "User not found")]
        public async Task<IActionResult> UpdateProfile(Guid id, [FromBody] UpdateProfileRequest request)
        {
            var result = await _userService.UpdateProfileAsync(id, request);
            if (!result)
                return NotFoundResponse<object>("User not found or update failed");

            return SuccessResponse(request, "Profile updated successfully");
        }

        #endregion

        #region Forgot / Reset Password

        [HttpPost("forgot-password")]
        [SwaggerOperation(
            Summary = "Forgot password",
            Description = "Initiates the password recovery process by sending a 6-digit verification code to the registered email address.\n\n" +
        "Usage steps:\n\n" +
        "Step 1: Call POST /forgot-password to send a 6-digit activation code to the user's email.\n\n" +
        "Step 2: Enter the received 6-digit code and call POST /reset-password to verify and activate the account."
        )]
        [SwaggerResponse(200, "Reset code sent successfully")]
        [SwaggerResponse(400, "Email not found or failed to send email")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var result = await _userService.ForgotPasswordAsync(request);
            if (!result.Success)
                return ErrorResponse<object>(result.ErrorMessage);

            return SuccessResponse(new { Message = "Password reset code sent to email" }, "Reset code sent successfully");
        }

        [HttpPost("reset-password")]
        [SwaggerOperation(
            Summary = "Reset password",        
            Description = "Completes the password recovery process by verifying the code and setting a new password. Invalidates all active sessions for security.\n\n" +
        "Usage steps:\n\n" +
        "Step 1: Call POST /forgot-password to send a 6-digit activation code to the user's email.\n\n" +
        "Step 2: Enter the received 6-digit code and call POST /reset-password to verify and activate the account."
        )]
        [SwaggerResponse(200, "Password reset successfully")]
        [SwaggerResponse(400, "Invalid email, incorrect code, or weak new password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var result = await _userService.ResetPasswordAsync(request);
            if (!result.Success)
                return ErrorResponse<object>(result.ErrorMessage);

            return SuccessResponse(new { Message = "Password reset successfully" }, "Password reset successfully");
        }



        #endregion
        
        #region Account Activation

        [HttpPost("send-activation-code")]
        [SwaggerOperation(
    Summary = "Send activation code",
    Description =
        "Sends a 6-digit activation code to the user's email to confirm their account.\n\n" +
        "Usage steps:\n\n" +
        "Step 1: Call POST /send-activation-code to send a 6-digit activation code to the user's email.\n\n" +
        "Step 2: Enter the received 6-digit code and call POST /verify-activation-code to verify and activate the account."
)]
        [SwaggerResponse(200, "Activation code sent successfully")]
        [SwaggerResponse(400, "User not found or email sending failed")]
        public async Task<IActionResult> SendActivationCode([FromBody] string email)
        {
            var result = await _userService.SendActivationCodeEmailConfirmedAsync(email);
            if (!result.Success)
                return ErrorResponse<object>(result.ErrorMessage);

            return SuccessResponse(new { Email = email }, "Activation code sent successfully");
        }

        [HttpPost("verify-activation-code")]
        [SwaggerOperation(
            Summary = "Verify activation code",
            Description = "Verifies the 6-digit code sent to the email and activates the account.\n\n" +
        "Usage steps:\n\n" +
        "Step 1: Call POST /send-activation-code to send a 6-digit activation code to the user's email.\n\n" +
        "Step 2: Enter the received 6-digit code and call POST /verify-activation-code to verify and activate the account."
        )]
        [SwaggerResponse(200, "Account activated successfully")]
        [SwaggerResponse(400, "Invalid or expired code")]
        public async Task<IActionResult> VerifyActivationCode([FromBody] VerifyEmailRequest request)
        {
            var result = await _userService.VerifyActivationCodeEmailConfirmedAsync(request);
            if (!result.Success)
                return ErrorResponse<object>(result.ErrorMessage);

            return SuccessResponse(new { Email = request.Email }, "Account activated successfully");
        }

        #endregion

        #region Role Management

        [Authorize]
        [HttpPost("upgrade-to-shop/{id}")]
        [SwaggerOperation(
            Summary = "Upgrade to Shop role",
            Description = "Upgrades the authenticated user's role from Customer to Shop. Condition: Email must be confirmed."
        )]
        [SwaggerResponse(200, "Upgraded to Shop successfully")]
        [SwaggerResponse(400, "Email not confirmed or user not found")]
        [SwaggerResponse(401, "Unauthorized")]
        public async Task<IActionResult> UpgradeToShop(Guid id)
        {
            var result = await _userService.UpgradeToShopAsync(id);
            if (!result.Success)
                return ErrorResponse<object>(result.ErrorMessage);

            return SuccessResponse(new { UserId = id }, "Upgraded to Shop successfully");
        }

        #endregion

    }
}
