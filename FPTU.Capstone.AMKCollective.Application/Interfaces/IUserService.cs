using System;
using System.Collections.Generic;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Auth;
using FPTU.Capstone.AMKCollective.Application.DTOs.User;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces
{
    public interface IUserService
    {
        IEnumerable<UserDto> GetAll();
        LoginResponse? Login(LoginRequest request);
        UserProfileDto? GetProfile(Guid userId);
        bool UpdateProfile(Guid userId, UpdateProfileRequest request);
        
        bool Register(RegisterRequest request, out string errorMessage);
        bool ChangePassword(Guid userId, ChangePasswordRequest request, out string errorMessage);
        
        Guid CreateUser(CreateUserRequest request, out string errorMessage);
        bool AdminUpdateUser(Guid userId, UpdateUserAdminRequest request, out string errorMessage);
        bool DeleteUser(Guid userId, out string errorMessage);
        bool BanUser(Guid userId, out string errorMessage);
        bool Logout(Guid userId, LogoutRequest request, out string errorMessage);
        RefreshTokenResponse? RefreshToken(Guid userId, RefreshTokenRequest request, out string errorMessage);
        bool VerifyEmail(VerifyEmailRequest request, out string errorMessage);
        bool ForgotPassword(ForgotPasswordRequest request, out string errorMessage);
        bool ResetPassword(ResetPasswordRequest request, out string errorMessage);
    }
}
