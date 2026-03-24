using Xunit;
using Moq;
using System.Threading.Tasks;
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
    public class UserServiceTests
    {
        // Helper method để set up mock UserService
        private UserService CreateUserService(IUnitOfWork mockUnitOfWork, IEmailService mockEmailService, IMapper mockMapper, ITokenService mockTokenService)
        {
            var securitySettings = Options.Create(new SecuritySettings());
            return new UserService(mockUnitOfWork, mockEmailService, mockMapper, mockTokenService, securitySettings);
        }

        // Helper method tạo hashed password giống UserService
        private string CreateMockPasswordHash(string password)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA512();
            var salt = System.Convert.ToBase64String(hmac.Key);
            var hash = System.Convert.ToBase64String(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password)));
            return $"{salt}:{hash}";
        }

        [Theory]
        [InlineData("user1@test.com", "password123", AccountStatus.Active, true, "Đăng nhập thành công")]
        [InlineData("user2@test.com", "wrongpassword", AccountStatus.Active, false, "Sai mật khẩu")]
        [InlineData("user3@test.com", "password123", AccountStatus.Suspended, false, "Tài khoản bị khóa")]
        [InlineData("nonexistent@test.com", "password123", AccountStatus.Active, false, "Email không tồn tại")]
        public async Task LoginAsync_VariousCases_ReturnsExpected(string email, string inputPassword, AccountStatus status, bool shouldSucceed, string testCase)
        {
            // Arrange
            var correctPassword = "password123";
            User user = null;

            if (email != "nonexistent@test.com")
            {
                user = new User 
                { 
                    Id = Guid.NewGuid(),
                    Email = email, 
                    HashedPassword = CreateMockPasswordHash(correctPassword),
                    Status = status,
                    Username = email.Split('@')[0]
                };
            }

            var mockUnitOfWork = new Mock<IUnitOfWork>();
            mockUnitOfWork.Setup(u => u.Users.GetByEmailAsync(email)).ReturnsAsync(user);
            mockUnitOfWork.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);

            var mockEmailService = new Mock<IEmailService>();
            var mockMapper = new Mock<IMapper>();
            mockMapper.Setup(m => m.Map<LoginResponse>(It.IsAny<User>()))
                .Returns((User u) => new LoginResponse { Email = u.Email });
            
            var mockTokenService = new Mock<ITokenService>();
            mockTokenService.Setup(t => t.CreateToken(It.IsAny<User>())).Returns("fake_jwt_token");

            var service = CreateUserService(mockUnitOfWork.Object, mockEmailService.Object, mockMapper.Object, mockTokenService.Object);

            var request = new LoginRequest { Email = email, Password = inputPassword };

            // Act
            var result = await service.LoginAsync(request);

            // Assert
            if (shouldSucceed)
            {
                Assert.NotNull(result);
                Assert.Equal(email, result.Email);
                Assert.NotNull(result.Token);
                Assert.NotNull(result.RefreshToken);
            }
            else
            {
                Assert.Null(result);
            }
        }

        [Theory]
        [InlineData("newuser", "newuser@test.com", true, "Đăng ký thành công")]
        [InlineData("existinguser", "existing@test.com", false, "Username đã tồn tại")]
        public async Task RegisterAsync_VariousCases_ReturnsExpected(string username, string email, bool shouldSucceed, string testCase)
        {
            // Arrange
            var mockUnitOfWork = new Mock<IUnitOfWork>();
            
            if (shouldSucceed)
            {
                mockUnitOfWork.Setup(u => u.Users.GetByUsernameAsync(username)).ReturnsAsync((User)null);
                mockUnitOfWork.Setup(u => u.Users.GetByEmailAsync(email)).ReturnsAsync((User)null);
            }
            else
            {
                mockUnitOfWork.Setup(u => u.Users.GetByUsernameAsync(username)).ReturnsAsync(new User { Username = username });
            }

            mockUnitOfWork.Setup(u => u.Users.GetRoleByNameAsync(It.IsAny<RoleType>())).ReturnsAsync(new Role { Name = RoleType.Customer });
            mockUnitOfWork.Setup(u => u.Users.AddAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            mockUnitOfWork.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);

            var mockEmailService = new Mock<IEmailService>();
            var mockMapper = new Mock<IMapper>();
            mockMapper.Setup(m => m.Map<User>(It.IsAny<RegisterRequest>())).Returns(new User());
            var mockTokenService = new Mock<ITokenService>();

            var service = CreateUserService(mockUnitOfWork.Object, mockEmailService.Object, mockMapper.Object, mockTokenService.Object);

            var request = new RegisterRequest 
            { 
                Username = username, 
                Email = email, 
                Password = "password123",
                FirstName = "Test",
                LastName = "User",
                Role = RoleType.Customer
            };

            // Act
            var result = await service.RegisterAsync(request);

            // Assert
            if (shouldSucceed)
            {
                Assert.True(result.Success);
                Assert.Empty(result.ErrorMessage);
            }
            else
            {
                Assert.False(result.Success);
                Assert.NotEmpty(result.ErrorMessage);
            }
        }
    }
}
