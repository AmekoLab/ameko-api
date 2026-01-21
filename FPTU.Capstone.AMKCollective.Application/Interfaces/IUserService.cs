using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Auth;
using FPTU.Capstone.AMKCollective.Application.DTOs.User;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces
{
    public interface IUserService
    {
        Task<IEnumerable<UserDto>> GetAllAsync();
        Task<LoginResponse?> LoginAsync(LoginRequest request);
        Task<UserProfileDto?> GetProfileAsync(Guid userId);
        Task<bool> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
        
        Task<(bool Success, string ErrorMessage)> RegisterAsync(RegisterRequest request);
        Task<(bool Success, string ErrorMessage)> ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
        
        Task<(Guid UserId, string ErrorMessage)> CreateUserAsync(CreateUserRequest request);
        Task<(bool Success, string ErrorMessage)> AdminUpdateUserAsync(Guid userId, UpdateUserAdminRequest request);
        Task<(bool Success, string ErrorMessage)> DeleteUserAsync(Guid userId);
        Task<(bool Success, string ErrorMessage)> BanUserAsync(Guid userId);
        Task<(bool Success, string ErrorMessage)> LogoutAsync(Guid userId, LogoutRequest request);
        Task<(RefreshTokenResponse? Response, string ErrorMessage)> RefreshTokenAsync(Guid userId, RefreshTokenRequest request);
        Task<(bool Success, string ErrorMessage)> VerifyActivationCodeEmailConfirmedAsync(VerifyEmailRequest request);
        Task<(bool Success, string ErrorMessage)> SendActivationCodeEmailConfirmedAsync(string email);
        Task<(bool Success, string ErrorMessage)> ForgotPasswordAsync(ForgotPasswordRequest request);
        Task<(bool Success, string ErrorMessage)> ResetPasswordAsync(ResetPasswordRequest request);
        Task<(bool Success, string ErrorMessage)> UpgradeToShopAsync(Guid userId);
        Task<bool> RevokeAllTokensAsync(Guid userId);
    }
}
