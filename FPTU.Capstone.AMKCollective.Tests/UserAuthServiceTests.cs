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
            mockUnitOfWork.Setup(u => u.Users.GetByUsernameAsync(It.IsAny<string>())).ReturnsAsync((User)null);
            mockUnitOfWork.Setup(u => u.Users.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((User)null);
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
    }
}
