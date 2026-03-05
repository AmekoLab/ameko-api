using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Auth;
using FPTU.Capstone.AMKCollective.Application.DTOs.User;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly IMapper _mapper;
        private readonly ITokenService _tokenService;
        private readonly SecuritySettings _securitySettings;

        public UserService(IUnitOfWork unitOfWork, IEmailService emailService, IMapper mapper, ITokenService tokenService, IOptions<SecuritySettings> securitySettings)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _mapper = mapper;
            _tokenService = tokenService;
            _securitySettings = securitySettings.Value;
        }

        private string GenerateRandom6DigitCode()
        {
            return new Random().Next(100000, 999999).ToString();
        }

        public async Task<PaginatedResult<UserResponse>> GetAllAsync(int pageNumber, int pageSize)
        {
            var (users, totalCount) = await _unitOfWork.Users.GetPagedAsync(pageNumber, pageSize);
            var userDtos = _mapper.Map<IEnumerable<UserResponse>>(users);
            
            return new PaginatedResult<UserResponse>
            {
                Items = userDtos,
                TotalCount = totalCount,
                CurrentPage = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<PaginatedResult<UserResponse>> SearchByNameAsync(string name, int pageNumber, int pageSize, CancellationToken ct = default)
        {
            var (users, totalCount) = await _unitOfWork.Users.SearchByNamePagedAsync(name, pageNumber, pageSize, ct);
            var userDtos = _mapper.Map<IEnumerable<UserResponse>>(users);

            return new PaginatedResult<UserResponse>
            {
                Items = userDtos,
                TotalCount = totalCount,
                CurrentPage = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<LoginResponse?> LoginAsync(LoginRequest request)
        {
            var user = await _unitOfWork.Users.GetByEmailAsync(request.Email);
            if (user == null || !VerifyPasswordHash(request.Password, user.HashedPassword))
                return null;
            if(user.Status == AccountStatus.Suspended)
                return null;

            var token = _tokenService.CreateToken(user);
            var refreshTokenRaw = await SaveRefreshTokenAsync(user);
            
            await _unitOfWork.CommitAsync();

            var response = _mapper.Map<LoginResponse>(user);
            response.Token = token;
            response.RefreshToken = refreshTokenRaw;

            return response;
        }

        public async Task<UserProfileResponse?> GetProfileAsync(Guid userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            return _mapper.Map<UserProfileResponse>(user);
        }

        public async Task<bool> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return false;

            _mapper.Map(request, user);
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.CommitAsync();
            return true;
        }

        public async Task<(bool Success, string ErrorMessage)> RegisterAsync(RegisterRequest request)
        {
            if (await _unitOfWork.Users.GetByUsernameAsync(request.Username) != null)
                return (false, "Username already exists");

            if (await _unitOfWork.Users.GetByEmailAsync(request.Email) != null)
                return (false, "Email already exists");

            var user = _mapper.Map<User>(request);
            user.HashedPassword = CreatePasswordHash(request.Password);
            user.Status = AccountStatus.Active; 

            var roleName = request.Role != 0 ? request.Role : RoleType.Customer;
            var role = await _unitOfWork.Users.GetRoleByNameAsync(roleName);
            if (role != null) user.Role = role;

            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.CommitAsync();

            return (true, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return (false, "User not found");

            if (!VerifyPasswordHash(request.OldPassword, user.HashedPassword))
                return (false, "Invalid old password");

            user.HashedPassword = CreatePasswordHash(request.NewPassword);
            await _unitOfWork.Users.UpdateAsync(user);
            
            // Security Trigger: Revoke all sessions on password change
            await RevokeAllTokensAsync(userId);
            
            await _unitOfWork.CommitAsync();

            return (true, string.Empty);
        }

        public async Task<(Guid UserId, string ErrorMessage)> CreateUserAsync(CreateUserRequest request)
        {
            if (await _unitOfWork.Users.GetByUsernameAsync(request.Username) != null)
                return (Guid.Empty, "Username already exists");

            var user = _mapper.Map<User>(request);
            user.HashedPassword = CreatePasswordHash(request.Password);
            user.EmailConfirmed = true;
            
            var role = await _unitOfWork.Users.GetRoleByNameAsync(request.Role);
            if (role != null) user.Role = role;

            await _unitOfWork.Users.AddAsync(user);
            await _unitOfWork.CommitAsync();

            return (user.Id, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> AdminUpdateUserAsync(Guid userId, UpdateUserAdminRequest request)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return (false, "User not found");

            _mapper.Map(request, user);
            if (request.Role.HasValue)
            {
                var role = await _unitOfWork.Users.GetRoleByNameAsync(request.Role.Value);
                if (role != null) user.Role = role;
            }

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.CommitAsync();

            return (true, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> DeleteUserAsync(Guid userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return (false, "User not found");

            await _unitOfWork.Users.DeleteAsync(user);
            await _unitOfWork.CommitAsync();

            return (true, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> BanUserAsync(Guid userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return (false, "User not found");

            user.Status = AccountStatus.Suspended;
            await _unitOfWork.Users.UpdateAsync(user);
            
            // Security Trigger: Revoke all sessions on account ban
            await RevokeAllTokensAsync(userId);
            
            await _unitOfWork.CommitAsync();

            return (true, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> LogoutAsync(Guid userId, LogoutRequest request)
        {
            var user = await _unitOfWork.Users.GetUserWithRefreshTokensAsync(userId);
            if (user == null) return (false, "User not found");

            var tokenToRemove = user.RefreshTokens.FirstOrDefault(rt => 
                VerifyPasswordHash(request.RefreshToken, $"{rt.TokenSalt}:{rt.TokenHash}"));

            if (tokenToRemove != null)
            {
                user.RefreshTokens.Remove(tokenToRemove);
                await _unitOfWork.CommitAsync();
            }

            return (true, string.Empty);
        }

        public async Task<(RefreshTokenResponse? Response, string ErrorMessage)> RefreshTokenAsync(Guid userId, RefreshTokenRequest request)
        {
            var user = await _unitOfWork.Users.GetUserWithRefreshTokensAsync(userId);
            if (user == null) return (null, "User not found");

            var tokenToRemove = user.RefreshTokens.FirstOrDefault(rt => 
                VerifyPasswordHash(request.RefreshToken, $"{rt.TokenSalt}:{rt.TokenHash}"));

            if (tokenToRemove == null) return (null, "Invalid Refresh Token");

            user.RefreshTokens.Remove(tokenToRemove);

            var newToken = _tokenService.CreateToken(user);
            var newRefreshTokenRaw = await SaveRefreshTokenAsync(user);
            
            await _unitOfWork.CommitAsync();

            return (new RefreshTokenResponse { Token = newToken, RefreshToken = newRefreshTokenRaw }, string.Empty);
        }

        private async Task<string> SaveRefreshTokenAsync(User user)
        {
            var refreshTokenRaw = Guid.NewGuid().ToString();
            var refreshTokenHash = CreatePasswordHash(refreshTokenRaw);
            var parts = refreshTokenHash.Split(':');

            var refreshTokenEntity = new RefreshToken
            {
                UserId = user.Id,
                TokenHash = parts[1],
                TokenSalt = parts[0]
            };

            // Enforce limit of tokens per user from settings
            if (user.RefreshTokens.Count >= _securitySettings.RefreshTokenLimit)
            {
                var oldestToken = user.RefreshTokens.OrderBy(rt => rt.CreatedAt).FirstOrDefault();
                if (oldestToken != null)
                {
                    user.RefreshTokens.Remove(oldestToken);
                }
            }

            await _unitOfWork.Users.AddRefreshTokenAsync(refreshTokenEntity);
            return refreshTokenRaw;
        }

        public async Task<(bool Success, string ErrorMessage)> SendActivationCodeEmailConfirmedAsync(string email)
        {
            var user = await _unitOfWork.Users.GetByEmailAsync(email);
            if (user == null) return (false, "User not found");

            var activationCode = GenerateRandom6DigitCode();
            user.VerificationCode = activationCode;
            user.VerificationCodeExpiryTime = DateTime.UtcNow.AddMinutes(_securitySettings.VerificationCodeExpiryMinutes);
            
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.CommitAsync();

            await _emailService.SendVerificationEmailAsync(email, activationCode);
            
            return (true, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> VerifyActivationCodeEmailConfirmedAsync(VerifyEmailRequest request)
        {
            var user = await _unitOfWork.Users.GetByEmailAsync(request.Email);
            if (user == null) return (false, "User not found");

            if (user.VerificationCode != request.Code)
                return (false, "Invalid verification code");

            if (user.VerificationCodeExpiryTime < DateTime.UtcNow)
                return (false, "Verification code expired");

            user.EmailConfirmed = true;
            user.VerificationCode = null;
            user.VerificationCodeExpiryTime = null;
            
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.CommitAsync();

            return (true, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            var user = await _unitOfWork.Users.GetByEmailAsync(request.Email);
            if (user == null) return (false, "Email not found");

            var resetCode = GenerateRandom6DigitCode();
            user.ResetPasswordToken = resetCode;
            
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.CommitAsync();

            // Send reset email
            await _emailService.SendPasswordResetEmailAsync(user.Email, resetCode);
            
            return (true, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> ResetPasswordAsync(ResetPasswordRequest request)
        {
            var user = await _unitOfWork.Users.GetByEmailAsync(request.Email);
            if (user == null || user.ResetPasswordToken != request.Code)
                return (false, "Invalid email or code");

            user.HashedPassword = CreatePasswordHash(request.NewPassword);
            user.ResetPasswordToken = null;
            
            // Security Trigger: Revoke all sessions on password reset
            await RevokeAllTokensAsync(user.Id);
            
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.CommitAsync();

            return (true, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> UpgradeToShopAsync(Guid userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return (false, "User not found");

            if (!user.EmailConfirmed)
                return (false, "Email must be confirmed before upgrading to Shop");

            var shopRole = await _unitOfWork.Users.GetRoleByNameAsync(RoleType.Shop);
            if (shopRole == null) return (false, "Shop role not found in system");

            user.Role = shopRole;
            user.RoleId = shopRole.Id;

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.CommitAsync();

            return (true, string.Empty);
        }

        public async Task<bool> RevokeAllTokensAsync(Guid userId)
        {
            await _unitOfWork.Users.RemoveAllRefreshTokensAsync(userId);
            return true;
        }

        private string CreatePasswordHash(string password)
        {
            using var hmac = new HMACSHA512();
            var salt = Convert.ToBase64String(hmac.Key);
            var hash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(password)));
            return $"{salt}:{hash}";
        }

        private bool VerifyPasswordHash(string password, string storedFullHash)
        {
            var parts = storedFullHash.Split(':');
            if (parts.Length != 2) return false;

            var salt = Convert.FromBase64String(parts[0]);
            var storedHash = parts[1];

            using var hmac = new HMACSHA512(salt);
            var computedHash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(password)));
            return computedHash == storedHash;
        }

        public async Task<bool> VerifyPasswordAsync(Guid userId, string password)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return false;
            return VerifyPasswordHash(password, user.HashedPassword);
        }

        public async Task<(bool Success, string ErrorMessage)> DowngradeToCustomerAsync(Guid userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null)
                return (false, "User not found");
            var customerRole = await _unitOfWork.Users.GetRoleByNameAsync(RoleType.Customer);
            if (customerRole == null)
                return (false, "Customer role not found");
            user.Role = customerRole;
            user.RoleId = customerRole.Id;
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.CommitAsync();
            return (true, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> SoftDeleteUserAsync(Guid userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return (false, "User not found");

            user.IsDeleted = true;
            user.Status = AccountStatus.Inactive;

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.CommitAsync();
            return (true, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> RestoreUserAsync(Guid userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return (false, "User not found");

            user.IsDeleted = false;
            user.Status = AccountStatus.Active;

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.CommitAsync();
            return (true, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> UnBanUserAsync(Guid userId)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) return (false, "User not found");

            user.Status = AccountStatus.Active;
            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.CommitAsync();
            return (true, string.Empty);
        }

    }
}
