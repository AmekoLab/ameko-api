using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Auth;
using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using FPTU.Capstone.AMKCollective.Application.DTOs.User;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
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

        /// <summary>
        /// Retrieves a paginated list of all users.
        /// </summary>
        /// <param name="currentPage">The page number to retrieve.</param>
        /// <param name="pageSize">The number of items per page.</param>
        /// <returns>A paginated result containing users.</returns>
        [SwaggerOperation(
            Summary = "Get all users",
            Description = "Returns a paginated list of all registered users. Admin role suggested for production." )]
        [SwaggerResponse(200, "Successfully retrieved list of users", typeof(PaginatedResult<UserResponse>))]
        [SwaggerResponse(401, "Unauthorized access")]
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] int currentPage = 1, [FromQuery] int pageSize = 10)
        {
            var users = await _userService.GetAllAsync(currentPage, pageSize);
            return SuccessResponse(users);
        }

        /// <summary>
        /// Searches users by first name or last name with pagination.
        /// </summary>
        /// <param name="name">The search term.</param>
        /// <param name="currentPage">The page number.</param>
        /// <param name="pageSize">The page size.</param>
        /// <returns>A paginated list of users matching the search criteria.</returns>
        [Authorize(Roles = "Admin")]
        [HttpGet("search")]
        [SwaggerOperation(Summary = "Search users by name", Description = "Searches for users by first name or last name. Requires Admin role.")]
        [SwaggerResponse(200, "Successfully retrieved search results", typeof(PaginatedResult<UserResponse>))]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - Requires Admin role")]
        public async Task<IActionResult> SearchByName([FromQuery] string? name, [FromQuery] int currentPage = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _userService.SearchByNameAsync(name ?? string.Empty, currentPage, pageSize);
            return SuccessResponse(result);
        }

        /// <summary>
        /// Authenticates a user and returns a JWT token.
        /// </summary>
        /// <param name="request">The login credentials.</param>
        /// <returns>A login response with tokens.</returns>
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
                return UnauthorizedResponse<LoginResponse>("Invalid email or password");

            return SuccessResponse(response);
        }


        #region Profile

        /// <summary>
        /// Retrieves the profile info for the authenticated user.
        /// </summary>
        /// <returns>The user profile details.</returns>
        [Authorize]
        [HttpGet("profile/{id}")]
        [SwaggerOperation(
            Summary = "Get user profile",
            Description = "Retrieve detailed profile information of a user by UserId"
        )]
        [SwaggerResponse(200, "Success", typeof(UserProfileResponse))]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(404, "User not found")]
        public async Task<IActionResult> GetProfile(Guid id)
        {
            var profile = await _userService.GetProfileAsync(id);
            if (profile == null)
                return NotFoundResponse<UserProfileResponse>("User not found");

            return SuccessResponse(profile);
        }

        #endregion

        #region Registration

        /// <summary>
        /// Registers a new user account.
        /// </summary>
        /// <param name="request">The registration details.</param>
        /// <returns>A success message if registered.</returns>
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

            return SuccessResponse(new { message = "User registered successfully" });
        }

        #endregion

        #region Password Management

        /// <summary>
        /// Changes the password for the authenticated user.
        /// </summary>
        /// <param name="request">The password change details.</param>
        /// <returns>A success message if changed.</returns>
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

        /// <summary>
        /// Logs out the authenticated user by revoking their refresh token.
        /// </summary>
        /// <param name="request">The logout request containing the refresh token.</param>
        /// <returns>A success message if logged out.</returns>
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

        /// <summary>
        /// Refreshes the JWT token using a valid refresh token.
        /// </summary>
        /// <param name="request">The refresh token.</param>
        /// <returns>A new token response.</returns>
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

        /// <summary>
        /// Admin: Creates a new confirmed user account.
        /// </summary>
        /// <param name="request">The user creation details.</param>
        /// <returns>The created user information.</returns>
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

        /// <summary>
        /// Admin: Updates an existing user's information.
        /// </summary>
        /// <param name="id">The ID of the user to update.</param>
        /// <param name="request">The updated user details.</param>
        /// <returns>A success response if updated.</returns>
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

        /// <summary>
        /// Admin: Deletes a user account.
        /// </summary>
        /// <param name="id">The ID of the user to delete.</param>
        /// <returns>A success response if deleted.</returns>
        [Authorize(Roles = "Admin")]
        [HttpDelete("admin/delete-user/{id}")]
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

        /// <summary>
        /// Admin: Bans a user account.
        /// </summary>
        /// <param name="id">The ID of the user to ban.</param>
        /// <returns>A success response if banned.</returns>
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

        [Authorize(Roles = "Admin")]
        [HttpPost("admin/soft-delete/{id}")]
        [SwaggerOperation(
            Summary = "Admin: Soft delete user",
            Description = "Marks a user account as deleted without removing data from the database. Requires Admin role."
        )]
        [SwaggerResponse(200, "User soft-deleted successfully")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - Requires Admin role")]
        [SwaggerResponse(404, "User not found")]
        public async Task<IActionResult> SoftDeleteUser(Guid id)
        {
            var result = await _userService.SoftDeleteUserAsync(id);
            if (!result.Success)
                return ErrorResponse<object>(result.ErrorMessage);
            return SuccessResponse(new { UserId = id }, "User soft-deleted successfully");
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("admin/restore/{id}")]
        [SwaggerOperation(
            Summary = "Admin: Restore user",
            Description = "Reactivates a previously banned or soft-deleted user account. Requires Admin role."
        )]
        [SwaggerResponse(200, "User restored successfully")]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - Requires Admin role")]
        [SwaggerResponse(404, "User not found")]
        public async Task<IActionResult> RestoreUser(Guid id)
        {
            var result = await _userService.RestoreUserAsync(id);
            if (!result.Success)
                return ErrorResponse<object>(result.ErrorMessage);
            return SuccessResponse(new { UserId = id }, "User restored successfully");
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("admin/un-ban/{id}")]
        [SwaggerOperation(
            Summary = "Admin: Get unbanned users",
            Description = "Retrieves a list of users who are currently not banned. Requires Admin role."
        )]
        [SwaggerResponse(200, "Successfully retrieved list of unbanned users", typeof(PaginatedResult<UserResponse>))]
        [SwaggerResponse(401, "Unauthorized")]
        [SwaggerResponse(403, "Forbidden - Requires Admin role")]
        public async Task<IActionResult> GetUnBannedUsers(Guid id)
        {
            var result = await _userService.UnBanUserAsync(id);
            if (!result.Success)
                return ErrorResponse<object>(result.ErrorMessage);
            return SuccessResponse(new { UserId = id }, "User unbanned successfully");
        }

        #endregion

        #region Profile Update

        /// <summary>
        /// Updates the profile of the authenticated user.
        /// </summary>
        /// <param name="request">The updated profile information.</param>
        /// <returns>A success message if updated.</returns>
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
            var success = await _userService.UpdateProfileAsync(id, request);
            if (!success)
                return NotFoundResponse<object>("User not found or update failed");

            return SuccessResponse(request, "Profile updated successfully");
        }

        #endregion

        #region Forgot / Reset Password

        /// <summary>
        /// Initiates the forgot password process.
        /// </summary>
        /// <param name="request">The request containing the user's email.</param>
        /// <returns>A success response if the reset code was sent.</returns>
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

        /// <summary>
        /// Resets the user's password using a verification code.
        /// </summary>
        /// <param name="request">The new password and reset code.</param>
        /// <returns>A success response if the password was reset.</returns>
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

        /// <summary>
        /// Sends an account activation code to the user's email.
        /// </summary>
        /// <param name="email">The email to send the code to.</param>
        /// <returns>A success response if the code was sent.</returns>
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

        /// <summary>
        /// Verifies the activation code and activates the account.
        /// </summary>
        /// <param name="request">The verification code and email.</param>
        /// <returns>A success response if the account was activated.</returns>
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

        /// <summary>
        /// Upgrades a user account to the 'Shop' role.
        /// </summary>
        /// <param name="id">The ID of the user to upgrade.</param>
        /// <returns>A success response if upgraded.</returns>
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
