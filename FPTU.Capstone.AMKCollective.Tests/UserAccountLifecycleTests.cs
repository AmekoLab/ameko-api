using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs.Auth;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Application.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.Extensions.Options;
using Moq;

namespace FPTU.Capstone.AMKCollective.Tests
{
    public class UserAccountLifecycleTests
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
        public async Task SendActivationCodeEmailConfirmedAsync_WhenUserNotFound_ReturnsFail()
        {
            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetByEmailAsync("missing@test.com")).ReturnsAsync((User?)null);

            var service = CreateUserService(unitOfWorkMock);

            var result = await service.SendActivationCodeEmailConfirmedAsync("missing@test.com");

            Assert.False(result.Success);
            Assert.Equal("User not found", result.ErrorMessage);
        }

        [Fact]
        public async Task SendActivationCodeEmailConfirmedAsync_WhenUserExists_SetsCodeAndSendsEmail()
        {
            var user = new User
            {
                Email = "user@test.com",
                Username = "user",
                FirstName = "U",
                LastName = "S",
                HashedPassword = CreatePasswordHash("123")
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetByEmailAsync(user.Email)).ReturnsAsync(user);
            unitOfWorkMock.Setup(u => u.Users.UpdateAsync(user)).Returns(Task.CompletedTask);
            unitOfWorkMock.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);

            var emailServiceMock = new Mock<IEmailService>();
            emailServiceMock
                .Setup(e => e.SendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var service = CreateUserService(unitOfWorkMock, emailServiceMock);

            var result = await service.SendActivationCodeEmailConfirmedAsync(user.Email);

            Assert.True(result.Success);
            Assert.Equal(string.Empty, result.ErrorMessage);
            Assert.False(string.IsNullOrWhiteSpace(user.VerificationCode));
            Assert.True(user.VerificationCodeExpiryTime.HasValue);

            emailServiceMock.Verify(e => e.SendVerificationEmailAsync(user.Email, It.IsAny<string>()), Times.Once);
            unitOfWorkMock.Verify(u => u.Users.UpdateAsync(user), Times.Once);
            unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task VerifyActivationCodeEmailConfirmedAsync_WhenCodeMismatch_ReturnsFail()
        {
            var user = new User
            {
                Email = "user@test.com",
                Username = "user",
                FirstName = "U",
                LastName = "S",
                HashedPassword = CreatePasswordHash("123"),
                VerificationCode = "111111",
                VerificationCodeExpiryTime = DateTime.UtcNow.AddMinutes(5)
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetByEmailAsync(user.Email)).ReturnsAsync(user);

            var service = CreateUserService(unitOfWorkMock);

            var result = await service.VerifyActivationCodeEmailConfirmedAsync(new VerifyEmailRequest
            {
                Email = user.Email,
                Code = "999999"
            });

            Assert.False(result.Success);
            Assert.Equal("Invalid verification code", result.ErrorMessage);
            unitOfWorkMock.Verify(u => u.Users.UpdateAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task VerifyActivationCodeEmailConfirmedAsync_WhenCodeExpired_ReturnsFail()
        {
            var user = new User
            {
                Email = "user@test.com",
                Username = "user",
                FirstName = "U",
                LastName = "S",
                HashedPassword = CreatePasswordHash("123"),
                VerificationCode = "111111",
                VerificationCodeExpiryTime = DateTime.UtcNow.AddMinutes(-1)
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetByEmailAsync(user.Email)).ReturnsAsync(user);

            var service = CreateUserService(unitOfWorkMock);

            var result = await service.VerifyActivationCodeEmailConfirmedAsync(new VerifyEmailRequest
            {
                Email = user.Email,
                Code = "111111"
            });

            Assert.False(result.Success);
            Assert.Equal("Verification code expired", result.ErrorMessage);
            unitOfWorkMock.Verify(u => u.Users.UpdateAsync(It.IsAny<User>()), Times.Never);
        }

        [Fact]
        public async Task VerifyActivationCodeEmailConfirmedAsync_WhenValidCode_ConfirmsEmailAndClearsCode()
        {
            var user = new User
            {
                Email = "user@test.com",
                Username = "user",
                FirstName = "U",
                LastName = "S",
                HashedPassword = CreatePasswordHash("123"),
                VerificationCode = "111111",
                VerificationCodeExpiryTime = DateTime.UtcNow.AddMinutes(10),
                EmailConfirmed = false
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetByEmailAsync(user.Email)).ReturnsAsync(user);
            unitOfWorkMock.Setup(u => u.Users.UpdateAsync(user)).Returns(Task.CompletedTask);
            unitOfWorkMock.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);

            var service = CreateUserService(unitOfWorkMock);

            var result = await service.VerifyActivationCodeEmailConfirmedAsync(new VerifyEmailRequest
            {
                Email = user.Email,
                Code = "111111"
            });

            Assert.True(result.Success);
            Assert.True(user.EmailConfirmed);
            Assert.Null(user.VerificationCode);
            Assert.Null(user.VerificationCodeExpiryTime);
            unitOfWorkMock.Verify(u => u.Users.UpdateAsync(user), Times.Once);
            unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task ForgotPasswordAsync_WhenEmailNotFound_ReturnsFail()
        {
            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetByEmailAsync("no@test.com")).ReturnsAsync((User?)null);

            var service = CreateUserService(unitOfWorkMock);

            var result = await service.ForgotPasswordAsync(new ForgotPasswordRequest { Email = "no@test.com" });

            Assert.False(result.Success);
            Assert.Equal("Email not found", result.ErrorMessage);
        }

        [Fact]
        public async Task ForgotPasswordAsync_WhenValidEmail_SetsResetCodeAndSendsEmail()
        {
            var user = new User
            {
                Email = "user@test.com",
                Username = "user",
                FirstName = "U",
                LastName = "S",
                HashedPassword = CreatePasswordHash("123")
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetByEmailAsync(user.Email)).ReturnsAsync(user);
            unitOfWorkMock.Setup(u => u.Users.UpdateAsync(user)).Returns(Task.CompletedTask);
            unitOfWorkMock.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);

            var emailServiceMock = new Mock<IEmailService>();
            emailServiceMock
                .Setup(e => e.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var service = CreateUserService(unitOfWorkMock, emailServiceMock);

            var result = await service.ForgotPasswordAsync(new ForgotPasswordRequest { Email = user.Email });

            Assert.True(result.Success);
            Assert.False(string.IsNullOrWhiteSpace(user.ResetPasswordToken));
            emailServiceMock.Verify(e => e.SendPasswordResetEmailAsync(user.Email, It.IsAny<string>()), Times.Once);
            unitOfWorkMock.Verify(u => u.Users.UpdateAsync(user), Times.Once);
            unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenInvalidEmailOrCode_ReturnsFail()
        {
            var user = new User
            {
                Email = "user@test.com",
                Username = "user",
                FirstName = "U",
                LastName = "S",
                HashedPassword = CreatePasswordHash("old"),
                ResetPasswordToken = "123456"
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetByEmailAsync(user.Email)).ReturnsAsync(user);

            var service = CreateUserService(unitOfWorkMock);

            var result = await service.ResetPasswordAsync(new ResetPasswordRequest
            {
                Email = user.Email,
                Code = "000000",
                NewPassword = "newPassword",
                ConfirmPassword = "newPassword"
            });

            Assert.False(result.Success);
            Assert.Equal("Invalid email or code", result.ErrorMessage);
            unitOfWorkMock.Verify(u => u.Users.RemoveAllRefreshTokensAsync(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task ResetPasswordAsync_WhenValid_ResetsPasswordAndRevokesTokens()
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "user@test.com",
                Username = "user",
                FirstName = "U",
                LastName = "S",
                HashedPassword = CreatePasswordHash("old"),
                ResetPasswordToken = "123456"
            };

            var oldHash = user.HashedPassword;

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetByEmailAsync(user.Email)).ReturnsAsync(user);
            unitOfWorkMock.Setup(u => u.Users.RemoveAllRefreshTokensAsync(user.Id)).Returns(Task.CompletedTask);
            unitOfWorkMock.Setup(u => u.Users.UpdateAsync(user)).Returns(Task.CompletedTask);
            unitOfWorkMock.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);

            var service = CreateUserService(unitOfWorkMock);

            var result = await service.ResetPasswordAsync(new ResetPasswordRequest
            {
                Email = user.Email,
                Code = "123456",
                NewPassword = "newPassword",
                ConfirmPassword = "newPassword"
            });

            Assert.True(result.Success);
            Assert.NotEqual(oldHash, user.HashedPassword);
            Assert.Null(user.ResetPasswordToken);
            unitOfWorkMock.Verify(u => u.Users.RemoveAllRefreshTokensAsync(user.Id), Times.Once);
            unitOfWorkMock.Verify(u => u.Users.UpdateAsync(user), Times.Once);
            unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task UpgradeToShopAsync_WhenUserNotFound_ReturnsFail()
        {
            var userId = Guid.NewGuid();
            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(userId)).ReturnsAsync((User?)null);

            var service = CreateUserService(unitOfWorkMock);

            var result = await service.UpgradeToShopAsync(userId);

            Assert.False(result.Success);
            Assert.Equal("User not found", result.ErrorMessage);
        }

        [Fact]
        public async Task UpgradeToShopAsync_WhenEmailNotConfirmed_ReturnsFail()
        {
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "user@test.com",
                Username = "user",
                FirstName = "U",
                LastName = "S",
                HashedPassword = CreatePasswordHash("123"),
                EmailConfirmed = false
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(userId)).ReturnsAsync(user);

            var service = CreateUserService(unitOfWorkMock);

            var result = await service.UpgradeToShopAsync(userId);

            Assert.False(result.Success);
            Assert.Equal("Email must be confirmed before upgrading to Shop", result.ErrorMessage);
        }

        [Fact]
        public async Task UpgradeToShopAsync_WhenShopRoleMissing_ReturnsFail()
        {
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "user@test.com",
                Username = "user",
                FirstName = "U",
                LastName = "S",
                HashedPassword = CreatePasswordHash("123"),
                EmailConfirmed = true
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(userId)).ReturnsAsync(user);
            unitOfWorkMock.Setup(u => u.Users.GetRoleByNameAsync(RoleType.Shop)).ReturnsAsync((Role?)null);

            var service = CreateUserService(unitOfWorkMock);

            var result = await service.UpgradeToShopAsync(userId);

            Assert.False(result.Success);
            Assert.Equal("Shop role not found in system", result.ErrorMessage);
        }

        [Fact]
        public async Task UpgradeToShopAsync_WhenShopProfileMissing_ReturnsFail()
        {
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "user@test.com",
                Username = "user",
                FirstName = "U",
                LastName = "S",
                HashedPassword = CreatePasswordHash("123"),
                EmailConfirmed = true
            };

            var shopRole = new Role { Id = Guid.NewGuid(), Name = RoleType.Shop };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(userId)).ReturnsAsync(user);
            unitOfWorkMock.Setup(u => u.Users.GetRoleByNameAsync(RoleType.Shop)).ReturnsAsync(shopRole);
            unitOfWorkMock.Setup(u => u.Shops.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((ShopProfile?)null);

            var service = CreateUserService(unitOfWorkMock);

            var result = await service.UpgradeToShopAsync(userId);

            Assert.False(result.Success);
            Assert.Equal("Shop profile is required before upgrading to Shop", result.ErrorMessage);
        }

        [Fact]
        public async Task UpgradeToShopAsync_WhenShopProfileIncomplete_ReturnsFail()
        {
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "user@test.com",
                Username = "user",
                FirstName = "U",
                LastName = "S",
                HashedPassword = CreatePasswordHash("123"),
                EmailConfirmed = true
            };

            var shopRole = new Role { Id = Guid.NewGuid(), Name = RoleType.Shop };
            var incompleteProfile = new ShopProfile
            {
                UserId = userId,
                ShopName = "My Shop",
                CitizenId = "0123456789"
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(userId)).ReturnsAsync(user);
            unitOfWorkMock.Setup(u => u.Users.GetRoleByNameAsync(RoleType.Shop)).ReturnsAsync(shopRole);
            unitOfWorkMock.Setup(u => u.Shops.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(incompleteProfile);

            var service = CreateUserService(unitOfWorkMock);

            var result = await service.UpgradeToShopAsync(userId);

            Assert.False(result.Success);
            Assert.StartsWith("Shop profile is incomplete. Missing:", result.ErrorMessage);
        }

        [Fact]
        public async Task UpgradeToShopAsync_WhenValid_UpdatesRoleAndCommits()
        {
            var userId = Guid.NewGuid();
            var roleId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "user@test.com",
                Username = "user",
                FirstName = "U",
                LastName = "S",
                HashedPassword = CreatePasswordHash("123"),
                EmailConfirmed = true
            };

            var shopRole = new Role { Id = roleId, Name = RoleType.Shop };
            var completeProfile = new ShopProfile
            {
                UserId = userId,
                ShopName = "AMK Artisan",
                Bio = "Custom artisan",
                Address = "HCM City",
                PhoneNumber = "0900000000",
                ContactEmail = "shop@test.com",
                CitizenId = "012345678901",
                TaxCode = "TAX-001",
                BankName = "Vietcombank",
                BankAccountNumber = "123456789",
                BankAccountName = "AMK Artisan"
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(userId)).ReturnsAsync(user);
            unitOfWorkMock.Setup(u => u.Users.GetRoleByNameAsync(RoleType.Shop)).ReturnsAsync(shopRole);
            unitOfWorkMock.Setup(u => u.Shops.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(completeProfile);
            unitOfWorkMock.Setup(u => u.Users.UpdateAsync(user)).Returns(Task.CompletedTask);
            unitOfWorkMock.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);

            var service = CreateUserService(unitOfWorkMock);

            var result = await service.UpgradeToShopAsync(userId);

            Assert.True(result.Success);
            Assert.Equal(roleId, user.RoleId);
            Assert.Equal(RoleType.Shop, user.Role.Name);
            unitOfWorkMock.Verify(u => u.Users.UpdateAsync(user), Times.Once);
            unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task BanUserAsync_WhenValid_SuspendsAndRevokesTokens()
        {
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "user@test.com",
                Username = "user",
                FirstName = "U",
                LastName = "S",
                HashedPassword = CreatePasswordHash("123"),
                Status = AccountStatus.Active
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(userId)).ReturnsAsync(user);
            unitOfWorkMock.Setup(u => u.Users.UpdateAsync(user)).Returns(Task.CompletedTask);
            unitOfWorkMock.Setup(u => u.Users.RemoveAllRefreshTokensAsync(userId)).Returns(Task.CompletedTask);
            unitOfWorkMock.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);

            var service = CreateUserService(unitOfWorkMock);

            var result = await service.BanUserAsync(userId);

            Assert.True(result.Success);
            Assert.Equal(AccountStatus.Suspended, user.Status);
            unitOfWorkMock.Verify(u => u.Users.RemoveAllRefreshTokensAsync(userId), Times.Once);
            unitOfWorkMock.Verify(u => u.Users.UpdateAsync(user), Times.Once);
            unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task SoftDeleteAndRestoreUserAsync_WhenValid_TogglesFlags()
        {
            var userId = Guid.NewGuid();
            var user = new User
            {
                Id = userId,
                Email = "user@test.com",
                Username = "user",
                FirstName = "U",
                LastName = "S",
                HashedPassword = CreatePasswordHash("123"),
                Status = AccountStatus.Active,
                IsDeleted = false
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            unitOfWorkMock.Setup(u => u.Users.GetByIdAsync(userId)).ReturnsAsync(user);
            unitOfWorkMock.Setup(u => u.Users.UpdateAsync(user)).Returns(Task.CompletedTask);
            unitOfWorkMock.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);

            var service = CreateUserService(unitOfWorkMock);

            var deleteResult = await service.SoftDeleteUserAsync(userId);
            Assert.True(deleteResult.Success);
            Assert.True(user.IsDeleted);
            Assert.Equal(AccountStatus.Inactive, user.Status);

            var restoreResult = await service.RestoreUserAsync(userId);
            Assert.True(restoreResult.Success);
            Assert.False(user.IsDeleted);
            Assert.Equal(AccountStatus.Active, user.Status);

            unitOfWorkMock.Verify(u => u.Users.UpdateAsync(user), Times.Exactly(2));
            unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Exactly(2));
        }
    }
}