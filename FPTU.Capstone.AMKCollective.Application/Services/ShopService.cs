using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Search;
using FPTU.Capstone.AMKCollective.Application.DTOs.Shop;
using FPTU.Capstone.AMKCollective.Application.Helpers;
using FPTU.Capstone.AMKCollective.Application.Interfaces.AI;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class ShopService : IShopService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStorageService _storage;
        private readonly IMapper _mapper;
        private readonly IUserService _userService;
        private readonly IWalletService _walletService;
        private readonly IEmailService _emailService;
        private readonly IAIService _aiService;
        private readonly ILogger<ShopService> _logger;
        private readonly ISearchHistoryQueue _searchQueue;
        private readonly INotificationService _notificationService;

        public ShopService(
            IUnitOfWork unitOfWork,
            IStorageService storage,
            IMapper mapper,
            IUserService userService,
            IWalletService walletService,
            IEmailService emailService,
            IAIService aiService,
            ILogger<ShopService> logger,
            ISearchHistoryQueue searchQueue,
            INotificationService notificationService)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
            _mapper = mapper;
            _userService = userService;
            _walletService = walletService;
            _emailService = emailService;
            _aiService = aiService;
            _logger = logger;
            _searchQueue = searchQueue;
            _notificationService = notificationService;
        }

        public async Task<ShopResponse> GetShopPublicProfileAsync(Guid shopId)
        {
            var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);

            if (shop == null
        || shop.Status == ShopStatus.Banned
        || shop.Status == ShopStatus.PendingApproval
        || shop.Status == ShopStatus.Rejected
        || shop.Status == ShopStatus.Inactive)
            {
                throw new KeyNotFoundException("Shop not found.");
            }

            var response = _mapper.Map<ShopResponse>(shop).ConvertDatesToLocal();
            response.FollowersCount = await _unitOfWork.Follows.GetFollowersCountAsync(shop.UserId);
            response.FollowingCount = await _unitOfWork.Follows.GetFollowingCountAsync(shop.UserId);
            return response;
        }

        public async Task<(IEnumerable<ShopResponse> Items, int TotalCount)> GetMarketplaceShopAsync(Guid? currentUserId, ShopFilterRequest filter)
        {
            var (items, total) = await _unitOfWork.Shops.GetFilteredShopsAsync(filter);
            if (currentUserId.HasValue && !string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                _searchQueue.TryQueueSearch(new SearchLogEvent(currentUserId.Value, filter.SearchTerm, "Shop"));
            }
            return (_mapper.Map<IEnumerable<ShopResponse>>(items).ConvertDatesToLocal(), total);
        }

        public async Task<ShopDetailResponse> GetMyShopAsync(Guid userId)
        {
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (shop == null)
            {
                throw new KeyNotFoundException("You do not have a shop yet.");
            }
            return _mapper.Map<ShopDetailResponse>(shop).ConvertDatesToLocal();
        }

        public async Task<ShopResponse> RegisterShopAsync(Guid userId, CreateShopRequest request)
        {
            var existingShop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (existingShop != null)
            {
                throw new InvalidOperationException("User already has a shop profile, please update .");
            }

            if (await _unitOfWork.Shops.IsShopNameExistsAsync(request.ShopName))
            {
                throw new ArgumentException("Shop name is already taken.");
            }

            if (await _unitOfWork.Shops.IsCitizenIdExistsAsync(request.CitizenId))
            {
                throw new ArgumentException("Citizen ID is already registered.");
            }

            string logoUrl = null;
            string bannerUrl = null;

            if (request.LogoImage != null)
            {
                logoUrl = await _storage.UploadAsync(
                    request.LogoImage.OpenReadStream(),
                    request.LogoImage.FileName,
                    "shops/logos");
            }

            if (request.BannerImage != null)
            {
                bannerUrl = await _storage.UploadAsync(
                    request.BannerImage.OpenReadStream(),
                    request.BannerImage.FileName,
                    "shops/banners");
            }

            var shop = new ShopProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ShopName = request.ShopName,
                Bio = request.Bio,
                Address = request.Address,
                PhoneNumber = request.PhoneNumber,
                ContactEmail = request.ContactEmail,
                CitizenId = request.CitizenId,
                TaxCode = request.TaxCode,
                BankName = request.BankName,
                BankAccountNumber = request.BankAccountNumber,
                BankAccountName = request.BankAccountName,
                LogoUrl = logoUrl,
                BannerUrl = bannerUrl,
                Status = ShopStatus.PendingApproval,
                IsActive = false, // Mặc định là đóng cửa để khi được duyệt shop có thời gian chuẩn bị
                CreatedAt = DateTime.UtcNow,
            };

            await _unitOfWork.Shops.CreateAsync(shop);
            await _unitOfWork.CommitAsync();

            try { await _aiService.SyncShopAsync(shop); } catch { /* Ignore */ }

            return _mapper.Map<ShopResponse>(shop).ConvertDatesToLocal();
        }

        public async Task PatchMyShopAsync(Guid userId, PatchShopRequest request)
        {
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (shop == null) throw new KeyNotFoundException("Shop not found");

            // Chỉ Active shop được phép PATCH non-critical fields
            if (shop.Status == ShopStatus.Banned)
                throw new InvalidOperationException("Your shop has been banned. You cannot update your profile.");

            if (shop.Status == ShopStatus.Inactive)
                throw new InvalidOperationException("Your shop is deactivated. You cannot update your profile.");

            if (shop.Status == ShopStatus.Rejected)
                throw new InvalidOperationException("Your shop registration was rejected. Please use the resubmit endpoint to modify your shop information.");

            if (shop.Status == ShopStatus.PendingApproval)
                throw new InvalidOperationException("Your shop is pending approval. You cannot update your profile at this time.");

            // Chỉ update những field non-critical
            if (!string.IsNullOrEmpty(request.Bio))
                shop.Bio = request.Bio;

            if (!string.IsNullOrEmpty(request.Address))
                shop.Address = request.Address;

            if (!string.IsNullOrEmpty(request.PhoneNumber))
                shop.PhoneNumber = request.PhoneNumber;

            if (!string.IsNullOrEmpty(request.ContactEmail))
                shop.ContactEmail = request.ContactEmail;

            if (request.LogoImage != null)
            {
                shop.LogoUrl = await _storage.UploadAsync(
                    request.LogoImage.OpenReadStream(),
                    request.LogoImage.FileName,
                    "shops/logos");
            }

            if (request.BannerImage != null)
            {
                shop.BannerUrl = await _storage.UploadAsync(
                    request.BannerImage.OpenReadStream(),
                    request.BannerImage.FileName,
                    "shops/banners");
            }

            await _unitOfWork.Shops.UpdateAsync(shop);
            await _unitOfWork.CommitAsync();

            try { await _aiService.SyncShopAsync(shop); } catch { /* Ignore */ }
        }

        public async Task UpdateMyShopRejectedAsync(Guid userId, UpdateShopRejectedRequest request)
        {
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (shop == null) throw new KeyNotFoundException("Shop not found");

            // Chỉ cho phép update khi shop bị Rejected
            if (shop.Status != ShopStatus.Rejected)
                throw new InvalidOperationException("You can only update shop information when status is Rejected.");

            // Validate ShopName nếu thay đổi
            if (!string.IsNullOrEmpty(request.ShopName) && request.ShopName != shop.ShopName)
            {
                if (await _unitOfWork.Shops.IsShopNameExistsAsync(request.ShopName))
                    throw new ArgumentException("Shop name is already taken.");
                shop.ShopName = request.ShopName;
            }

            // Validate CitizenId nếu thay đổi
            if (!string.IsNullOrEmpty(request.CitizenId) && request.CitizenId != shop.CitizenId)
            {
                if (await _unitOfWork.Shops.IsCitizenIdExistsAsync(request.CitizenId))
                    throw new ArgumentException("Citizen ID is already registered.");
                shop.CitizenId = request.CitizenId;
            }

            // Update các field critical
            if (!string.IsNullOrEmpty(request.TaxCode))
                shop.TaxCode = request.TaxCode;

            if (!string.IsNullOrEmpty(request.BankName))
                shop.BankName = request.BankName;

            if (!string.IsNullOrEmpty(request.BankAccountNumber))
                shop.BankAccountNumber = request.BankAccountNumber;

            if (!string.IsNullOrEmpty(request.BankAccountName))
                shop.BankAccountName = request.BankAccountName;

            // Update non-critical fields
            if (!string.IsNullOrEmpty(request.Bio))
                shop.Bio = request.Bio;

            if (!string.IsNullOrEmpty(request.Address))
                shop.Address = request.Address;

            if (!string.IsNullOrEmpty(request.PhoneNumber))
                shop.PhoneNumber = request.PhoneNumber;

            if (!string.IsNullOrEmpty(request.ContactEmail))
                shop.ContactEmail = request.ContactEmail;

            if (request.LogoImage != null)
            {
                shop.LogoUrl = await _storage.UploadAsync(
                    request.LogoImage.OpenReadStream(),
                    request.LogoImage.FileName,
                    "shops/logos");
            }

            if (request.BannerImage != null)
            {
                shop.BannerUrl = await _storage.UploadAsync(
                    request.BannerImage.OpenReadStream(),
                    request.BannerImage.FileName,
                    "shops/banners");
            }

            // Reset resubmit count và submit lại
            shop.Status = ShopStatus.PendingApproval;
            shop.ResubmitCount++;
            shop.LastResubmitTime = DateTime.UtcNow;

            await _unitOfWork.Shops.UpdateAsync(shop);
            await _unitOfWork.CommitAsync();

            try { await _aiService.SyncShopAsync(shop); } catch { /* Ignore */ }
        }

        public async Task<(IEnumerable<ShopDetailResponse> Items, int TotalCount)> GetShopForAdminAsync(string? searchTerm, ShopStatus? status, int page, int size)
        {
            var (items, total) = await _unitOfWork.Shops.GetShopsAsync(searchTerm, status, page, size);
            return (_mapper.Map<IEnumerable<ShopDetailResponse>>(items).ConvertDatesToLocal(), total);
        }

        public async Task ApproveShopAsync(Guid shopId, ApproveShopRequest request)
        {
            var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);
            if (shop == null) throw new KeyNotFoundException("Shop not found");

            // Chỉ cho phép Approve/Reject qua endpoint này
            if (request.Status != ShopStatus.Active && request.Status != ShopStatus.Rejected)
                throw new ArgumentException("Only Active or Rejected status is allowed through this endpoint.");

            // Chỉ approve/reject shop đang PendingApproval hoặc Inactive (Admin muốn mở lại)
            if (shop.Status != ShopStatus.PendingApproval && shop.Status != ShopStatus.Inactive)
                throw new InvalidOperationException($"Cannot approve/reject shop with current status: {shop.Status}. Only PendingApproval or Inactive shops can be processed.");

            if (request.Status == ShopStatus.Active)
            {
                var upgradeResult = await _userService.UpgradeToShopAsync(shop.UserId);
                if (!upgradeResult.Success)
                {
                    throw new InvalidOperationException($"Cannot approve shop. User upgrade failed: {upgradeResult.ErrorMessage}");
                }
                shop.Status = ShopStatus.Active;
                shop.AdminNote = request.AdminNote;
                shop.ResubmitCount = 0;
                shop.LastResubmitTime = null;

                // Tạo Wallet cho Shop mới được duyệt — nếu chưa có sẽ tạo mới, nếu rồi thì bỏ qua
                await _walletService.CreateWalletAsync(shop.UserId);
            }
            else // Rejected
            {
                shop.Status = ShopStatus.Rejected;
                shop.AdminNote = request.AdminNote;
            }

            await _unitOfWork.Shops.UpdateAsync(shop);
            await _unitOfWork.CommitAsync();

            if (request.Status == ShopStatus.Active)
            {
                await _notificationService.SendNotificationAsync(
                    shop.UserId,
                    "Shop của bạn đã được duyệt",
                    $"Chúc mừng! Shop \"{shop.ShopName}\" đã được duyệt và có thể bắt đầu hoạt động. Vui lòng nạp tối thiểu 2.000.000 ₫ vào ví để kích hoạt shop.",
                    nameof(NotificationType.ShopStatusUpdated),
                    shopId.ToString(),
                    "Shop");
            }
            else
            {
                await _notificationService.SendNotificationAsync(
                    shop.UserId,
                    "Hồ sơ shop không được duyệt",
                    $"Shop \"{shop.ShopName}\" chưa được duyệt." + (string.IsNullOrWhiteSpace(request.AdminNote) ? "" : $" Lý do: {request.AdminNote}"),
                    nameof(NotificationType.ShopStatusUpdated),
                    shopId.ToString(),
                    "Shop");
            }

            // Gửi email thông báo kết quả duyệt cho shop
            try
            {
                var user = await _unitOfWork.Users.GetByIdAsync(shop.UserId);
                if (user != null)
                {
                    if (request.Status == ShopStatus.Active)
                    {
                        await _emailService.SendEmailAsync(
                            user.Email,
                            "Your shop has been approved!",
                            $"Congratulations! Your shop \"{shop.ShopName}\" has been approved and is now active on AMK Collective.\n\n" +
                            $"To start selling, please top up your shop wallet with a minimum of 2,000,000 VND. " +
                            $"This balance is required to activate your shop and covers compensation obligations.\n\n" +
                            $"You can top up via Stripe (credit card) or VNPay at: Wallet > Top Up.");
                    }
                    else
                    {
                        await _emailService.SendEmailAsync(
                            user.Email,
                            "Your shop application was not approved",
                            $"Unfortunately, your shop \"{shop.ShopName}\" was not approved at this time.\n\n" +
                            (string.IsNullOrWhiteSpace(request.AdminNote) ? "" : $"Reason: {request.AdminNote}\n\n") +
                            $"You may resubmit your application after addressing the issues.");
                    }
                }
            }
            catch { /* Email failure không nên block luồng approve */ }

            try { await _aiService.SyncShopAsync(shop); } catch { /* Ignore */ }
        }

        [Obsolete("This API is deprecated and disabled.")]

        /// <summary>
        /// Admin: Deactivate shop — thay đổi Status sang Inactive + force IsActive = false
        /// </summary>
        public async Task AdminDeactivateShopAsync(Guid shopId)
        {
            var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);
            if (shop == null) throw new KeyNotFoundException("Shop not found");

            if (shop.Status != ShopStatus.Active)
                throw new InvalidOperationException("Only active shops can be deactivated by admin.");

            shop.Status = ShopStatus.Inactive;  // Admin quyết định Status
            shop.IsActive = false;               // Force inactive
            await _unitOfWork.Shops.UpdateAsync(shop);
            await _unitOfWork.CommitAsync();
        }

        /// <summary>
        /// Shop Owner: Tạm nghỉ bán — chỉ thay đổi IsActive, KHÔNG thay đổi Status
        /// </summary>
        public async Task DeactivateMyShopAsync(Guid userId)
        {
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (shop == null) throw new KeyNotFoundException("Shop not found");

            if (shop.Status != ShopStatus.Active)
                throw new InvalidOperationException("Only active shops can be deactivated.");

            if (!shop.IsActive)
                throw new InvalidOperationException("Shop is already deactivated.");

            shop.IsActive = false;  // Chủ shop quyết định IsActive
            await _unitOfWork.Shops.UpdateAsync(shop);
            await _unitOfWork.CommitAsync();
        }

        /// <summary>
        /// Shop Owner: Mở lại shop — chỉ thay đổi IsActive, KHÔNG thay đổi Status
        /// </summary>
        public async Task ReactivateMyShopAsync(Guid userId)
        {
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (shop == null) throw new KeyNotFoundException("Shop not found");

            if (shop.Status != ShopStatus.Active)
                throw new InvalidOperationException("Cannot reactivate. Shop status must be Active (approved by admin).");

            if (shop.IsActive)
                throw new InvalidOperationException("Shop is already active.");

            // Yêu cầu balance tối thiểu 2,000,000 VND để hoạt động (đảm bảo khả năng đền bù)
            var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(userId);
            if (wallet == null || wallet.Balance < 2_000_000m)
                throw new InvalidOperationException("Insufficient wallet balance. Your shop must have at least 2,000,000 VND to activate. Please top up your wallet first.");

            shop.IsActive = true;
            await _unitOfWork.Shops.UpdateAsync(shop);
            await _unitOfWork.CommitAsync();
        }

        public async Task<(IEnumerable<ShopResponse> Items, int TotalCount)> GetAllPendingApprovalShopAsync(int page, int size)
        {
            var (shops, total) = await _unitOfWork.Shops.GetAllPendingApprovalShopAsync(page, size);
            return (_mapper.Map<IEnumerable<ShopResponse>>(shops).ConvertDatesToLocal(), total);
        }

        public async Task BannedShopAsync(Guid shopId)
        {
            var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);
            if (shop == null) throw new KeyNotFoundException("Shop not found");

            // Không cho ban shop đã ban hoặc đang PendingApproval
            if (shop.Status == ShopStatus.Banned)
                throw new InvalidOperationException("Shop is already banned.");

            if (shop.Status == ShopStatus.PendingApproval)
                throw new InvalidOperationException("Cannot ban a shop that is pending approval. Please reject it instead.");

            shop.Status = ShopStatus.Banned;
            shop.IsActive = false;
            await _unitOfWork.Shops.UpdateAsync(shop);
            await _unitOfWork.CommitAsync();

            await _notificationService.SendNotificationAsync(
                shop.UserId,
                "Shop của bạn đã bị cấm hoạt động",
                $"Shop \"{shop.ShopName}\" đã bị cấm hoạt động trên nền tảng. Vui lòng liên hệ admin để biết thêm chi tiết.",
                nameof(NotificationType.ShopStatusUpdated),
                shopId.ToString(),
                "Shop");
        }

        public async Task UnbanShopAsync(Guid shopId)
        {
            var shop = await _unitOfWork.Shops.GetByIdAsync(shopId);
            if (shop == null) throw new KeyNotFoundException("Shop not found");

            // Check cho chắc chắn
            if (shop.Status != ShopStatus.Banned)
            {
                throw new InvalidOperationException("Shop is not currently banned.");
            }

            // Confirm: unban → PendingApproval (shop cần được Admin duyệt lại trước khi hoạt động)
            shop.Status = ShopStatus.PendingApproval;

            // Giữ trạng thái ĐÓNG CỬa (IsActive = false) — owner mở lại thủ công sau khi được duyệt
            shop.IsActive = false;

            shop.AdminNote = $"Unbanned at {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC. Pending re-approval.";

            await _unitOfWork.Shops.UpdateAsync(shop);
            await _unitOfWork.CommitAsync();

            await _notificationService.SendNotificationAsync(
                shop.UserId,
                "Shop của bạn đã được gỡ lệnh cấm",
                $"Shop \"{shop.ShopName}\" đã được gỡ lệnh cấm và đang chờ duyệt lại. Vui lòng chờ admin xét duyệt trước khi tiếp tục hoạt động.",
                nameof(NotificationType.ShopStatusUpdated),
                shopId.ToString(),
                "Shop");
        }

        public async Task UpdateBankInfoAsync(Guid userId, UpdateBankInfoRequest request)
        {
            // 1. Lấy thông tin User và Shop
            var user = await _unitOfWork.Users.GetByIdAsync(userId);
            if (user == null) throw new KeyNotFoundException("User not found");

            var shop = await _unitOfWork.Shops.GetByUserIdAsync(userId);
            if (shop == null) throw new KeyNotFoundException("Shop profile not found");

            // Chỉ Active shop mới được phép cập nhật thông tin ngân hàng
            if (shop.Status == ShopStatus.Banned)
                throw new InvalidOperationException("Your shop has been banned. You cannot update bank information.");

            if (shop.Status == ShopStatus.Inactive)
                throw new InvalidOperationException("Your shop is deactivated. You cannot update bank information.");

            if (shop.Status == ShopStatus.Rejected)
                throw new InvalidOperationException("Your shop registration was rejected. You cannot update bank information.");

            if (shop.Status == ShopStatus.PendingApproval)
                throw new InvalidOperationException("Your shop is pending approval. You cannot update bank information at this time.");

            // 2. [LỚP BẢO MẬT 1] Kiểm tra Mật khẩu đăng nhập
            var isPasswordCorrect = await _userService.VerifyPasswordAsync(userId, request.CurrentPassword);
            if (!isPasswordCorrect)
            {
                throw new UnauthorizedAccessException("Incorrect password");
            }

            // 3. [LỚP BẢO MẬT 2] Kiểm tra Mã PIN Ví
            var isPinCorrect = await _walletService.VerifyPinAsync(userId, request.WalletPin);
            if (!isPinCorrect)
            {
                throw new UnauthorizedAccessException("Incorrect Pin");
            }

            // 4. Nếu qua cả 2 lớp -> Update thông tin ngân hàng
            shop.BankName = request.BankName;
            shop.BankAccountNumber = request.BankAccountNumber;
            shop.BankAccountName = request.BankAccountName;

            await _unitOfWork.Shops.UpdateAsync(shop); 
            await _unitOfWork.CommitAsync();

            // 5. Gửi Email cảnh báo (Security Alert)
            try
            {
                string subject = "[AMK Collective] Security Alert: Bank Account Updated";
                string body = $@"
                    <h3>Bank Information Changed</h3>
                    <p>Hello {user.Username},</p>
                    <p>The bank account information for your shop <b>{shop.ShopName}</b> has just been updated.</p>
                    <ul>
                        <li><b>New Bank:</b> {request.BankName}</li>
                        <li><b>Account Number:</b> ****{request.BankAccountNumber.Substring(Math.Max(0, request.BankAccountNumber.Length - 4))}</li>
                    </ul>
                    <p style='color:red'>If you did not perform this action, please contact support and change your password immediately.</p>
                ";
                await _emailService.SendEmailAsync(user.Email, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send security alert email for shop {ShopName}", shop.ShopName);
            }
        }
    }
}