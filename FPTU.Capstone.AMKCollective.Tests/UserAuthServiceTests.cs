using Xunit;
using Moq;
using FPTU.Capstone.AMKCollective.Application.Services;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Application.DTOs.Auth;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using AutoMapper;
using Microsoft.Extensions.Options;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;

namespace FPTU.Capstone.AMKCollective.Tests
{
    public class UserAuthServiceTests
    {
        private UserService CreateUserService(IUnitOfWork mockUnitOfWork, IEmailService mockEmailService, IMapper mockMapper, ITokenService mockTokenService)
        {
            var securitySettings = Options.Create(new SecuritySettings());
            return new UserService(mockUnitOfWork, mockEmailService, mockMapper, mockTokenService, securitySettings);
        }

        private string CreatePasswordHash(string password)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA512();
            var salt = System.Convert.ToBase64String(hmac.Key);
            var hash = System.Convert.ToBase64String(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password)));
            return $"{salt}:{hash}";
        }

        [Fact]
        public async Task LoginAsync_WithValidCredentials_ReturnsLoginResponse()
        {
            // Arrange
            var email = "test@example.com";
            var password = "password123";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                HashedPassword = CreatePasswordHash(password),
                Status = AccountStatus.Active,
                EmailConfirmed = true,
                Username = "testuser"
            };

            var mockUnitOfWork = new Mock<IUnitOfWork>();
            mockUnitOfWork.Setup(u => u.Users.GetByEmailAsync(email)).ReturnsAsync(user);
            mockUnitOfWork.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);

            var mockEmailService = new Mock<IEmailService>();
            var mockMapper = new Mock<IMapper>();
            mockMapper.Setup(m => m.Map<LoginResponse>(It.IsAny<User>()))
                .Returns(new LoginResponse { Email = email });

            var mockTokenService = new Mock<ITokenService>();
            mockTokenService.Setup(t => t.CreateToken(It.IsAny<User>())).Returns("jwt_token");

            var service = CreateUserService(mockUnitOfWork.Object, mockEmailService.Object, mockMapper.Object, mockTokenService.Object);

            // Act
            var result = await service.LoginAsync(new LoginRequest { Email = email, Password = password });

            // Assert
            Assert.NotNull(result);
            Assert.Equal(email, result.Email);
            Assert.True(result.EmailConfirmed);
            Assert.Equal(AccountStatus.Active.ToString(), result.AccountStatus);
        }

        [Fact]
        public async Task LoginAsync_WithUnverifiedEmail_ReturnsLoginResponseForFrontendRedirect()
        {
            // Arrange
            var email = "unverified@example.com";
            var password = "password123";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                HashedPassword = CreatePasswordHash(password),
                Status = AccountStatus.Active,
                EmailConfirmed = false,
                Username = "unverified"
            };

            var mockUnitOfWork = new Mock<IUnitOfWork>();
            mockUnitOfWork.Setup(u => u.Users.GetByEmailAsync(email)).ReturnsAsync(user);
            mockUnitOfWork.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);

            var mockEmailService = new Mock<IEmailService>();
            var mockMapper = new Mock<IMapper>();
            mockMapper.Setup(m => m.Map<LoginResponse>(It.IsAny<User>()))
                .Returns(new LoginResponse { Email = email });

            var mockTokenService = new Mock<ITokenService>();
            mockTokenService.Setup(t => t.CreateToken(It.IsAny<User>())).Returns("jwt_token");

            var service = CreateUserService(mockUnitOfWork.Object, mockEmailService.Object, mockMapper.Object, mockTokenService.Object);

            // Act
            var result = await service.LoginAsync(new LoginRequest { Email = email, Password = password });

            // Assert
            Assert.NotNull(result);
            Assert.False(result.EmailConfirmed);
            Assert.Equal(AccountStatus.Active.ToString(), result.AccountStatus);
        }

        [Fact]
        public async Task LoginAsync_WithWrongPassword_ReturnsNull()
        {
            // Arrange
            var email = "test@example.com";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                HashedPassword = CreatePasswordHash("correctpassword"),
                Status = AccountStatus.Active,
                Username = "testuser"
            };

            var mockUnitOfWork = new Mock<IUnitOfWork>();
            mockUnitOfWork.Setup(u => u.Users.GetByEmailAsync(email)).ReturnsAsync(user);

            var mockEmailService = new Mock<IEmailService>();
            var mockMapper = new Mock<IMapper>();
            var mockTokenService = new Mock<ITokenService>();

            var service = CreateUserService(mockUnitOfWork.Object, mockEmailService.Object, mockMapper.Object, mockTokenService.Object);

            // Act
            var result = await service.LoginAsync(new LoginRequest { Email = email, Password = "wrongpassword" });

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task LoginAsync_WithSuspendedAccount_ReturnsNull()
        {
            // Arrange
            var email = "test@example.com";
            var password = "password123";
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                HashedPassword = CreatePasswordHash(password),
                Status = AccountStatus.Suspended,
                Username = "testuser"
            };

            var mockUnitOfWork = new Mock<IUnitOfWork>();
            mockUnitOfWork.Setup(u => u.Users.GetByEmailAsync(email)).ReturnsAsync(user);

            var mockEmailService = new Mock<IEmailService>();
            var mockMapper = new Mock<IMapper>();
            var mockTokenService = new Mock<ITokenService>();

            var service = CreateUserService(mockUnitOfWork.Object, mockEmailService.Object, mockMapper.Object, mockTokenService.Object);

            // Act
            var result = await service.LoginAsync(new LoginRequest { Email = email, Password = password });

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task RegisterAsync_WithValidData_ReturnsSuccess()
        {
            // Arrange
            var mockUnitOfWork = new Mock<IUnitOfWork>();
            mockUnitOfWork.Setup(u => u.Users.GetByUsernameAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
            mockUnitOfWork.Setup(u => u.Users.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);
            mockUnitOfWork.Setup(u => u.Users.GetRoleByNameAsync(It.IsAny<RoleType>())).ReturnsAsync(new Role { Name = RoleType.Customer });
            mockUnitOfWork.Setup(u => u.Users.AddAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            mockUnitOfWork.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);

            var mockEmailService = new Mock<IEmailService>();
            var mockMapper = new Mock<IMapper>();
            mockMapper.Setup(m => m.Map<User>(It.IsAny<RegisterRequest>())).Returns(new User());
            var mockTokenService = new Mock<ITokenService>();

            var service = CreateUserService(mockUnitOfWork.Object, mockEmailService.Object, mockMapper.Object, mockTokenService.Object);

            // Act
            var result = await service.RegisterAsync(new RegisterRequest
            {
                Username = "newuser",
                Email = "new@example.com",
                Password = "password123",
                FirstName = "John",
                LastName = "Doe",
                Role = RoleType.Customer
            });

            // Assert
            Assert.True(result.Success);
            Assert.Empty(result.ErrorMessage);
        }

        [Fact]
        public async Task RegisterAsync_WithExistingUsername_ReturnsFail()
        {
            // Arrange
            var mockUnitOfWork = new Mock<IUnitOfWork>();
            mockUnitOfWork.Setup(u => u.Users.GetByUsernameAsync("existinguser"))
                .ReturnsAsync(new User { Username = "existinguser" });

            var mockEmailService = new Mock<IEmailService>();
            var mockMapper = new Mock<IMapper>();
            var mockTokenService = new Mock<ITokenService>();

            var service = CreateUserService(mockUnitOfWork.Object, mockEmailService.Object, mockMapper.Object, mockTokenService.Object);

            // Act
            var result = await service.RegisterAsync(new RegisterRequest
            {
                Username = "existinguser",
                Email = "new@example.com",
                Password = "password123",
                FirstName = "John",
                LastName = "Doe",
                Role = RoleType.Customer
            });

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Username already exists", result.ErrorMessage);
        }

        [Fact]
        public async Task LoginAsync_WithUserNotFound_ReturnsNull()
        {
            var mockUnitOfWork = new Mock<IUnitOfWork>();
            mockUnitOfWork.Setup(u => u.Users.GetByEmailAsync("missing@test.com")).ReturnsAsync((User?)null);

            var service = CreateUserService(
                mockUnitOfWork.Object,
                new Mock<IEmailService>().Object,
                new Mock<IMapper>().Object,
                new Mock<ITokenService>().Object);

            var result = await service.LoginAsync(new LoginRequest { Email = "missing@test.com", Password = "123" });

            Assert.Null(result);
        }

        [Fact]
        public async Task RegisterAsync_WithExistingEmail_ReturnsFail()
        {
            var mockUnitOfWork = new Mock<IUnitOfWork>();
            mockUnitOfWork.Setup(u => u.Users.GetByUsernameAsync("newuser")).ReturnsAsync((User?)null);
            mockUnitOfWork.Setup(u => u.Users.GetByEmailAsync("exists@test.com"))
                .ReturnsAsync(new User { Email = "exists@test.com", Username = "exists" });

            var service = CreateUserService(
                mockUnitOfWork.Object,
                new Mock<IEmailService>().Object,
                new Mock<IMapper>().Object,
                new Mock<ITokenService>().Object);

            var result = await service.RegisterAsync(new RegisterRequest
            {
                Username = "newuser",
                Email = "exists@test.com",
                Password = "password123",
                FirstName = "A",
                LastName = "B",
                Role = RoleType.Customer
            });

            Assert.False(result.Success);
            Assert.Contains("Email already exists", result.ErrorMessage);
        }

        [Fact]
        public async Task ChangePasswordAsync_WithUserNotFound_ReturnsFail()
        {
            var userId = Guid.NewGuid();
            var mockUnitOfWork = new Mock<IUnitOfWork>();
            mockUnitOfWork.Setup(u => u.Users.GetByIdAsync(userId)).ReturnsAsync((User?)null);

            var service = CreateUserService(
                mockUnitOfWork.Object,
                new Mock<IEmailService>().Object,
                new Mock<IMapper>().Object,
                new Mock<ITokenService>().Object);

            var result = await service.ChangePasswordAsync(userId, new ChangePasswordRequest
            {
                OldPassword = "old",
                NewPassword = "new",
                ConfirmNewPassword = "new"
            });

            Assert.False(result.Success);
            Assert.Equal("User not found", result.ErrorMessage);
        }

        [Fact]
        public async Task ChangePasswordAsync_WithWrongOldPassword_ReturnsFail()
        {
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Username = "john",
                Email = "john@test.com",
                HashedPassword = CreatePasswordHash("correctOld")
            };

            var mockUnitOfWork = new Mock<IUnitOfWork>();
            mockUnitOfWork.Setup(u => u.Users.GetByIdAsync(userId)).ReturnsAsync(user);

            var service = CreateUserService(
                mockUnitOfWork.Object,
                new Mock<IEmailService>().Object,
                new Mock<IMapper>().Object,
                new Mock<ITokenService>().Object);

            var result = await service.ChangePasswordAsync(userId, new ChangePasswordRequest
            {
                OldPassword = "wrongOld",
                NewPassword = "new",
                ConfirmNewPassword = "new"
            });

            Assert.False(result.Success);
            Assert.Equal("Invalid old password", result.ErrorMessage);
            mockUnitOfWork.Verify(u => u.Users.RemoveAllRefreshTokensAsync(It.IsAny<Guid>()), Times.Never);
            mockUnitOfWork.Verify(u => u.CommitAsync(), Times.Never);
        }

        [Fact]
        public async Task ChangePasswordAsync_WithValidOldPassword_UpdatesPasswordAndRevokesTokens()
        {
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Username = "john",
                Email = "john@test.com",
                HashedPassword = CreatePasswordHash("oldPassword")
            };

            var mockUnitOfWork = new Mock<IUnitOfWork>();
            mockUnitOfWork.Setup(u => u.Users.GetByIdAsync(userId)).ReturnsAsync(user);
            mockUnitOfWork.Setup(u => u.Users.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            mockUnitOfWork.Setup(u => u.Users.RemoveAllRefreshTokensAsync(userId)).Returns(Task.CompletedTask);
            mockUnitOfWork.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);

            var service = CreateUserService(
                mockUnitOfWork.Object,
                new Mock<IEmailService>().Object,
                new Mock<IMapper>().Object,
                new Mock<ITokenService>().Object);

            var result = await service.ChangePasswordAsync(userId, new ChangePasswordRequest
            {
                OldPassword = "oldPassword",
                NewPassword = "newPassword",
                ConfirmNewPassword = "newPassword"
            });

            Assert.True(result.Success);
            Assert.NotEqual(CreatePasswordHash("oldPassword"), user.HashedPassword);
            mockUnitOfWork.Verify(u => u.Users.RemoveAllRefreshTokensAsync(userId), Times.Once);
            mockUnitOfWork.Verify(u => u.Users.UpdateAsync(user), Times.Once);
            mockUnitOfWork.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task RefreshTokenAsync_WhenUserNotFound_ReturnsFail()
        {
            var userId = Guid.NewGuid();
            var mockUnitOfWork = new Mock<IUnitOfWork>();
            mockUnitOfWork.Setup(u => u.Users.GetUserWithRefreshTokensAsync(userId)).ReturnsAsync((User?)null);

            var service = CreateUserService(
                mockUnitOfWork.Object,
                new Mock<IEmailService>().Object,
                new Mock<IMapper>().Object,
                new Mock<ITokenService>().Object);

            var result = await service.RefreshTokenAsync(userId, new RefreshTokenRequest { RefreshToken = "any" });

            Assert.Null(result.Response);
            Assert.Equal("User not found", result.ErrorMessage);
        }

        [Fact]
        public async Task RefreshTokenAsync_WhenInvalidToken_ReturnsFail()
        {
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Username = "john",
                Email = "john@test.com",
                RefreshTokens = new List<RefreshToken>()
            };

            var existingRaw = "valid_refresh";
            var existingHash = CreatePasswordHash(existingRaw).Split(':');
            user.RefreshTokens.Add(new RefreshToken
            {
                UserId = userId,
                TokenSalt = existingHash[0],
                TokenHash = existingHash[1]
            });

            var mockUnitOfWork = new Mock<IUnitOfWork>();
            mockUnitOfWork.Setup(u => u.Users.GetUserWithRefreshTokensAsync(userId)).ReturnsAsync(user);

            var service = CreateUserService(
                mockUnitOfWork.Object,
                new Mock<IEmailService>().Object,
                new Mock<IMapper>().Object,
                new Mock<ITokenService>().Object);

            var result = await service.RefreshTokenAsync(userId, new RefreshTokenRequest { RefreshToken = "wrong" });

            Assert.Null(result.Response);
            Assert.Equal("Invalid Refresh Token", result.ErrorMessage);
        }

        [Fact]
        public async Task RefreshTokenAsync_WithValidToken_ReturnsNewTokenAndRefreshToken()
        {
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Username = "john",
                Email = "john@test.com",
                RefreshTokens = new List<RefreshToken>()
            };

            var existingRaw = "valid_refresh";
            var existingHash = CreatePasswordHash(existingRaw).Split(':');
            user.RefreshTokens.Add(new RefreshToken
            {
                UserId = userId,
                TokenSalt = existingHash[0],
                TokenHash = existingHash[1]
            });

            var mockUnitOfWork = new Mock<IUnitOfWork>();
            mockUnitOfWork.Setup(u => u.Users.GetUserWithRefreshTokensAsync(userId)).ReturnsAsync(user);
            mockUnitOfWork.Setup(u => u.Users.AddRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);
            mockUnitOfWork.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);

            var mockTokenService = new Mock<ITokenService>();
            mockTokenService.Setup(t => t.CreateToken(user)).Returns("new_jwt");

            var service = CreateUserService(
                mockUnitOfWork.Object,
                new Mock<IEmailService>().Object,
                new Mock<IMapper>().Object,
                mockTokenService.Object);

            var result = await service.RefreshTokenAsync(userId, new RefreshTokenRequest { RefreshToken = existingRaw });

            Assert.NotNull(result.Response);
            Assert.Equal("new_jwt", result.Response!.Token);
            Assert.False(string.IsNullOrWhiteSpace(result.Response.RefreshToken));
            Assert.Equal(string.Empty, result.ErrorMessage);
            Assert.Empty(user.RefreshTokens);

            mockUnitOfWork.Verify(u => u.Users.AddRefreshTokenAsync(It.IsAny<RefreshToken>()), Times.Once);
            mockUnitOfWork.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task LogoutAsync_WithMatchingRefreshToken_RemovesTokenAndCommits()
        {
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Username = "john",
                Email = "john@test.com",
                RefreshTokens = new List<RefreshToken>()
            };

            var existingRaw = "raw_logout_token";
            var hashParts = CreatePasswordHash(existingRaw).Split(':');
            user.RefreshTokens.Add(new RefreshToken
            {
                UserId = userId,
                TokenSalt = hashParts[0],
                TokenHash = hashParts[1]
            });

            var mockUnitOfWork = new Mock<IUnitOfWork>();
            mockUnitOfWork.Setup(u => u.Users.GetUserWithRefreshTokensAsync(userId)).ReturnsAsync(user);
            mockUnitOfWork.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);

            var service = CreateUserService(
                mockUnitOfWork.Object,
                new Mock<IEmailService>().Object,
                new Mock<IMapper>().Object,
                new Mock<ITokenService>().Object);

            var result = await service.LogoutAsync(userId, new LogoutRequest { RefreshToken = existingRaw });

            Assert.True(result.Success);
            Assert.Equal(string.Empty, result.ErrorMessage);
            Assert.Empty(user.RefreshTokens);
            mockUnitOfWork.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task LogoutAsync_WhenUserNotFound_ReturnsFail()
        {
            var userId = Guid.NewGuid();
            var mockUnitOfWork = new Mock<IUnitOfWork>();
            mockUnitOfWork.Setup(u => u.Users.GetUserWithRefreshTokensAsync(userId)).ReturnsAsync((User?)null);

            var service = CreateUserService(
                mockUnitOfWork.Object,
                new Mock<IEmailService>().Object,
                new Mock<IMapper>().Object,
                new Mock<ITokenService>().Object);

            var result = await service.LogoutAsync(userId, new LogoutRequest { RefreshToken = "any" });

            Assert.False(result.Success);
            Assert.Equal("User not found", result.ErrorMessage);
        }
    }
}
