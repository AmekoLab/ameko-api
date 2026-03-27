using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs.Auth;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.DTOs.User;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Application.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.Extensions.Options;
using Moq;

namespace FPTU.Capstone.AMKCollective.Tests
{
    public class UserServiceBusinessTests
    {
        private static UserService CreateUserService(
            Mock<IUnitOfWork> unitOfWorkMock,
            Mock<IEmailService>? emailServiceMock = null,
            Mock<IMapper>? mapperMock = null,
            Mock<ITokenService>? tokenServiceMock = null,
            SecuritySettings? securitySettings = null)
        {
            emailServiceMock ??= new Mock<IEmailService>();
            mapperMock ??= new Mock<IMapper>();
            tokenServiceMock ??= new Mock<ITokenService>();
            securitySettings ??= new SecuritySettings();

            return new UserService(
                unitOfWorkMock.Object,
                emailServiceMock.Object,
                mapperMock.Object,
                tokenServiceMock.Object,
                Options.Create(securitySettings));
        }

        private static string CreatePasswordHash(string password)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA512();
            var salt = Convert.ToBase64String(hmac.Key);
            var hash = Convert.ToBase64String(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password)));
            return $"{salt}:{hash}";
        }

        [Fact]
        public async Task LoginAsync_WhenRefreshTokenLimitReached_RemovesOldestBeforeAddingNew()
        {
            var userId = Guid.NewGuid();
            var oldToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenSalt = "oldSalt",
                TokenHash = "oldHash",
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            };

            var user = new User
            {
                Id = userId,
                Username = "john",
                Email = "john@test.com",
                FirstName = "John",
                LastName = "Doe",
                Status = AccountStatus.Active,
                HashedPassword = CreatePasswordHash("password123"),
                RefreshTokens = new List<RefreshToken> { oldToken }
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetByEmailAsync(user.Email)).ReturnsAsync(user);
            unitOfWorkMock.Setup(u => u.Users.AddRefreshTokenAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);
            unitOfWorkMock.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);

            var mapperMock = new Mock<IMapper>();
            mapperMock.Setup(m => m.Map<LoginResponse>(user)).Returns(new LoginResponse { Email = user.Email, Username = user.Username });

            var tokenServiceMock = new Mock<ITokenService>();
            tokenServiceMock.Setup(t => t.CreateToken(user)).Returns("jwt-token");

            var service = CreateUserService(
                unitOfWorkMock,
                mapperMock: mapperMock,
                tokenServiceMock: tokenServiceMock,
                securitySettings: new SecuritySettings { RefreshTokenLimit = 1, VerificationCodeExpiryMinutes = 15 });

            var result = await service.LoginAsync(new LoginRequest { Email = user.Email, Password = "password123" });

            Assert.NotNull(result);
            Assert.Equal("jwt-token", result!.Token);
            Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
            Assert.DoesNotContain(oldToken, user.RefreshTokens);

            unitOfWorkMock.Verify(u => u.Users.AddRefreshTokenAsync(It.Is<RefreshToken>(rt => rt.UserId == userId)), Times.Once);
            unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsMappedPaginatedResult()
        {
            var users = new List<User>
            {
                new User { Id = Guid.NewGuid(), FirstName = "A", LastName = "One", Username = "a1", Email = "a1@test.com", HashedPassword = "x" },
                new User { Id = Guid.NewGuid(), FirstName = "B", LastName = "Two", Username = "b2", Email = "b2@test.com", HashedPassword = "x" }
            };

            var mapped = new List<UserResponse>
            {
                new UserResponse { Id = users[0].Id, FirstName = "A", LastName = "One", Username = "a1", Email = "a1@test.com" },
                new UserResponse { Id = users[1].Id, FirstName = "B", LastName = "Two", Username = "b2", Email = "b2@test.com" }
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetPagedAsync(2, 10)).ReturnsAsync((users, 57));

            var mapperMock = new Mock<IMapper>();
            mapperMock.Setup(m => m.Map<IEnumerable<UserResponse>>(users)).Returns(mapped);

            var service = CreateUserService(unitOfWorkMock, mapperMock: mapperMock);

            var result = await service.GetAllAsync(2, 10);

            Assert.Equal(57, result.TotalCount);
            Assert.Equal(2, result.CurrentPage);
            Assert.Equal(10, result.PageSize);
            Assert.Equal(2, result.Items.Count());
        }

        [Fact]
        public async Task SearchByNameAsync_ReturnsMappedPaginatedResult()
        {
            var users = new List<User>
            {
                new User { Id = Guid.NewGuid(), FirstName = "John", LastName = "A", Username = "johnA", Email = "a@test.com", HashedPassword = "x" }
            };

            var mapped = new List<UserResponse>
            {
                new UserResponse { Id = users[0].Id, FirstName = "John", LastName = "A", Username = "johnA", Email = "a@test.com" }
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.SearchByNamePagedAsync("john", 1, 20, It.IsAny<CancellationToken>()))
                .ReturnsAsync((users, 1));

            var mapperMock = new Mock<IMapper>();
            mapperMock.Setup(m => m.Map<IEnumerable<UserResponse>>(users)).Returns(mapped);

            var service = CreateUserService(unitOfWorkMock, mapperMock: mapperMock);

            var result = await service.SearchByNameAsync("john", 1, 20);

            Assert.Equal(1, result.TotalCount);
            Assert.Single(result.Items);
        }

        [Fact]
        public async Task CreateUserAsync_WhenUsernameExists_ReturnsFail()
        {
            var request = new CreateUserRequest
            {
                Username = "exists",
                Password = "password123",
                Email = "e@test.com",
                FirstName = "E",
                LastName = "X",
                Role = RoleType.Customer
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetByUsernameAsync(request.Username)).ReturnsAsync(new User { Username = request.Username, Email = "old@test.com", HashedPassword = "x", FirstName = "Old", LastName = "User" });

            var service = CreateUserService(unitOfWorkMock);

            var result = await service.CreateUserAsync(request);

            Assert.Equal(Guid.Empty, result.UserId);
            Assert.Equal("Username already exists", result.ErrorMessage);
        }

        [Fact]
        public async Task CreateUserAsync_WhenValid_SetsRoleAndEmailConfirmed()
        {
            var request = new CreateUserRequest
            {
                Username = "newuser",
                Password = "password123",
                Email = "new@test.com",
                FirstName = "New",
                LastName = "User",
                Role = RoleType.Admin
            };

            var mappedUser = new User
            {
                Id = Guid.NewGuid(),
                Username = request.Username,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                HashedPassword = "temp"
            };

            var role = new Role { Id = Guid.NewGuid(), Name = RoleType.Admin };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetByUsernameAsync(request.Username)).ReturnsAsync((User?)null);
            unitOfWorkMock.Setup(u => u.Users.GetRoleByNameAsync(request.Role)).ReturnsAsync(role);
            unitOfWorkMock.Setup(u => u.Users.AddAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            unitOfWorkMock.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);

            var mapperMock = new Mock<IMapper>();
            mapperMock.Setup(m => m.Map<User>(request)).Returns(mappedUser);

            var service = CreateUserService(unitOfWorkMock, mapperMock: mapperMock);

            var result = await service.CreateUserAsync(request);

            Assert.Equal(mappedUser.Id, result.UserId);
            Assert.Equal(string.Empty, result.ErrorMessage);
            Assert.True(mappedUser.EmailConfirmed);
            Assert.Equal(RoleType.Admin, mappedUser.Role.Name);
            unitOfWorkMock.Verify(u => u.Users.AddAsync(mappedUser), Times.Once);
        }

        [Fact]
        public async Task AdminUpdateUserAsync_WhenRoleProvidedButNotFound_StillUpdatesBasicInfo()
        {
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Username = "user",
                Email = "old@test.com",
                FirstName = "Old",
                LastName = "Name",
                HashedPassword = "x"
            };

            var request = new UpdateUserAdminRequest
            {
                FirstName = "New",
                LastName = "Name",
                Role = RoleType.Shop
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(userId)).ReturnsAsync(user);
            unitOfWorkMock.Setup(u => u.Users.GetRoleByNameAsync(RoleType.Shop)).ReturnsAsync((Role?)null);
            unitOfWorkMock.Setup(u => u.Users.UpdateAsync(user)).Returns(Task.CompletedTask);
            unitOfWorkMock.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);

            var mapperMock = new Mock<IMapper>();
            mapperMock.Setup(m => m.Map(request, user)).Callback<UpdateUserAdminRequest, User>((src, dest) =>
            {
                dest.FirstName = src.FirstName!;
                dest.LastName = src.LastName!;
            });

            var service = CreateUserService(unitOfWorkMock, mapperMock: mapperMock);

            var result = await service.AdminUpdateUserAsync(userId, request);

            Assert.True(result.Success);
            Assert.Equal("New", user.FirstName);
            Assert.Equal("Name", user.LastName);
            unitOfWorkMock.Verify(u => u.Users.UpdateAsync(user), Times.Once);
            unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateProfileAsync_WhenUserNotFound_ReturnsFalse()
        {
            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var userId = Guid.NewGuid();
            unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(userId)).ReturnsAsync((User?)null);

            var service = CreateUserService(unitOfWorkMock);

            var result = await service.UpdateProfileAsync(userId, new UpdateProfileRequest { FirstName = "A" });

            Assert.False(result);
        }

        [Fact]
        public async Task VerifyPasswordAsync_ReturnsExpectedForCorrectAndWrongPassword()
        {
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Username = "john",
                Email = "john@test.com",
                FirstName = "John",
                LastName = "Doe",
                HashedPassword = CreatePasswordHash("secret")
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(userId)).ReturnsAsync(user);

            var service = CreateUserService(unitOfWorkMock);

            var valid = await service.VerifyPasswordAsync(userId, "secret");
            var invalid = await service.VerifyPasswordAsync(userId, "wrong");

            Assert.True(valid);
            Assert.False(invalid);
        }

        [Fact]
        public async Task DowngradeToCustomerAsync_WhenCustomerRoleMissing_ReturnsFail()
        {
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Username = "shop",
                Email = "shop@test.com",
                FirstName = "Shop",
                LastName = "User",
                HashedPassword = "x"
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(userId)).ReturnsAsync(user);
            unitOfWorkMock.Setup(u => u.Users.GetRoleByNameAsync(RoleType.Customer)).ReturnsAsync((Role?)null);

            var service = CreateUserService(unitOfWorkMock);

            var result = await service.DowngradeToCustomerAsync(userId);

            Assert.False(result.Success);
            Assert.Equal("Customer role not found", result.ErrorMessage);
        }
    }
}