using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs.Commission;
using FPTU.Capstone.AMKCollective.Application.DTOs.Reputation;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class CommissionService : ICommissionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly CommissionSettings _commissionSettings;
        private readonly INotificationService _notificationService;
        private readonly ReputationSettings _reputationSettings;
        private readonly IReputationService _reputationService;

        public CommissionService(IUnitOfWork unitOfWork, IMapper mapper, IOptions<CommissionSettings> commissionSettings, INotificationService notificationService, IOptions<ReputationSettings> reputationSettings, IReputationService reputationService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _commissionSettings = commissionSettings.Value;
            _notificationService = notificationService;
            _reputationSettings = reputationSettings.Value;
            _reputationService = reputationService;
        }

        public async Task<IEnumerable<CommissionRequestResponse>> GetUserRequestsAsync(Guid userId)
        {
            var requests = await _unitOfWork.CommissionRequests.GetByUserIdAsync(userId);
            return _mapper.Map<IEnumerable<CommissionRequestResponse>>(requests);
        }
        public async Task<CommissionRequestResponse?> GetRequestDetailAsync(Guid requestId, Guid currentUserId)
        {
            var request = await _unitOfWork.CommissionRequests.GetByIdAsync(requestId);
            if (request == null) return null;

            var response = _mapper.Map<CommissionRequestResponse>(request);
            // Chỉ chủ request hoặc Shop được target mới xem được nội dung đầy đủ đối với targeted request
            if (request.UserId != currentUserId)
            {
                var shop = await _unitOfWork.Shops.GetByUserIdAsync(currentUserId);

                if (shop != null)
                {
                    // Nếu là targeted request và shop này không phải target → block
                    if (request.Status == CommissionStatus.PendingTarget && request.TargetedShopId != shop.Id)
                        return null; // Trả 404 trín controller

                    response.Quotes = response.Quotes.Where(q => q.ShopId == shop.Id).ToList();
                }
                else
                {
                    // User thường — không cho xem nếu không phải chủ request
                    return null;
                }
            }

            return response;
        }

        public async Task<CommissionRequestResponse?> GetRequestDetailAsync(Guid requestId)
        {
            var request = await _unitOfWork.CommissionRequests.GetByIdAsync(requestId);
            return _mapper.Map<CommissionRequestResponse>(request);
        }

        public async Task<IEnumerable<CommissionRequestResponse>> GetOpenPoolRequestsAsync()
        {
            var requests = await _unitOfWork.CommissionRequests.GetOpenPoolRequestsAsync();
            return _mapper.Map<IEnumerable<CommissionRequestResponse>>(requests);
        }

        public async Task<(bool Success, Guid? RequestId, string ErrorMessage)> CreateRequestAsync(Guid userId, CreateCommissionRequest requestDto)
        {
            var request = _mapper.Map<CommissionRequest>(requestDto);
            request.UserId = userId;

            if (!requestDto.IsDraft)
            {
                var activeCount = await _unitOfWork.CommissionRequests.CountActiveRequestsForUserAsync(userId);
                if (activeCount >= _commissionSettings.MaxActiveRequests)
                {
                    return (false, null, "You have reached the maximum number of active commission requests.");
                }
            }

            if (requestDto.IsDraft)
            {
                request.Status = CommissionStatus.Draft;
                request.TargetedShopId = null;
            }
            else
            {
                // Có chỉ định thì chờ shop không thì ném lên chợ chung
                request.Status = requestDto.TargetedShopId.HasValue
                    ? CommissionStatus.PendingTarget
                    : CommissionStatus.OpenPool;
            }

            var shopResponseHours = requestDto.ShopResponseWindowHours ?? _commissionSettings.DefaultShopResponseHours;
            if (shopResponseHours <= 0)
            {
                shopResponseHours = _commissionSettings.DefaultShopResponseHours;
            }

            var customerResponseHours = requestDto.CustomerResponseWindowHours ?? _commissionSettings.DefaultCustomerResponseHours;
            if (customerResponseHours <= 0)
            {
                customerResponseHours = _commissionSettings.DefaultCustomerResponseHours;
            }

            request.ShopResponseWindowHours = shopResponseHours;
            request.CustomerResponseWindowHours = customerResponseHours;
            request.ShopResponseDeadlineAt = requestDto.IsDraft ? null : DateTime.UtcNow.AddHours(shopResponseHours);
            request.ReminderCount = 0;
            request.LastReminderAt = null;

            await _unitOfWork.CommissionRequests.AddAsync(request);
            await _unitOfWork.CommitAsync();

            return (true, request.Id, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> UpdateRequestAsync(Guid userId, Guid requestId, UpdateCommissionRequest requestDto)
        {
            var request = await _unitOfWork.CommissionRequests.GetByIdAsync(requestId);
            if (request == null) return (false, "The request does not exist.");

            if (request.UserId != userId) return (false, "You do not have permission to perform actions on this request.");

            if (request.Status != CommissionStatus.Draft)
            {
                return (false, "Only draft requests can be edited.");
            }

            if (request.Quotes.Any(q => q.Status == QuoteStatus.PendingUserDecision || q.Status == QuoteStatus.Accepted))
            {
                return (false, "This request already has active offers and cannot be edited.");
            }

            request.Title = requestDto.Title;
            request.Description = requestDto.Description;
            request.ReferenceImages = requestDto.ReferenceImages;
            request.MinBudget = requestDto.MinBudget;
            request.MaxBudget = requestDto.MaxBudget;
            request.Quantity = requestDto.Quantity;
            request.TargetedShopId = requestDto.TargetedShopId;

            if (requestDto.ShopResponseWindowHours.HasValue)
            {
                request.ShopResponseWindowHours = requestDto.ShopResponseWindowHours.Value;
            }

            if (requestDto.CustomerResponseWindowHours.HasValue)
            {
                request.CustomerResponseWindowHours = requestDto.CustomerResponseWindowHours.Value;
            }

            await _unitOfWork.CommissionRequests.UpdateAsync(request);
            await _unitOfWork.CommitAsync();

            return (true, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> SubmitQuoteAsync(Guid shopUserId, Guid requestId, SubmitQuoteRequest quoteDto)
        {
            // Lấy profile Shop của user đang đăng nhập
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(shopUserId);
            if (shop == null) return (false, "Only shop accounts are allowed to submit quotations.");

            // [Fix #2] Không cho Shop bị ban/inactive/pending submit báo giá
            if (shop.Status == ShopStatus.Banned)
                return (false, "Your shop has been banned and cannot submit quotations.");
            if (shop.Status == ShopStatus.Inactive || !shop.IsActive)
                return (false, "Your shop is currently inactive. Please reactivate before submitting quotations.");
            if (shop.Status != ShopStatus.Active)
                return (false, "Your shop must be Active to submit quotations.");

            var request = await _unitOfWork.CommissionRequests.GetByIdAsync(requestId);
            if (request == null) return (false, "The request does not exist.");
            if (request.UserId == shopUserId)
            {
                return (false, "You cannot submit a quotation for your own commission request.");
            }

            if (request.Status == CommissionStatus.Completed || request.Status == CommissionStatus.Canceled)
            {
                return (false, "This request is closed and cannot accept additional quotations.");
            }

            if (request.Status == CommissionStatus.Draft || request.Status == CommissionStatus.RejectedByShop)
            {
                return (false, "This request is not published for quotations yet.");
            }

            // Check shop đã báo giá chưa (tránh báo giá trùng)
            if (request.Quotes.Any(q => q.ShopId == shop.Id && q.Status != QuoteStatus.Revoked))
            {
                return (false, "Your shop has already submitted a quotation for this request.");
            }

            var quote = _mapper.Map<CommissionQuote>(quoteDto);
            quote.CommissionRequestId = requestId;
            quote.ShopId = shop.Id;
            quote.Status = QuoteStatus.PendingUserDecision;
            quote.ExpiredAt = DateTime.UtcNow.AddDays(_commissionSettings.QuoteValidityDays);
            var customerDecisionHours = request.CustomerResponseWindowHours > 0
                ? request.CustomerResponseWindowHours
                : _commissionSettings.DefaultCustomerResponseHours;
            quote.CustomerDecisionDeadlineAt = DateTime.UtcNow.AddHours(customerDecisionHours);

            // Chuyển trạng thái của Request sang Quoted (nếu chưa Quoted)
            if (request.Status == CommissionStatus.OpenPool || request.Status == CommissionStatus.PendingTarget)
            {
                request.Status = CommissionStatus.Quoted;
                await _unitOfWork.CommissionRequests.UpdateAsync(request);
            }

            await _unitOfWork.CommissionQuotes.AddAsync(quote);
            await _unitOfWork.CommitAsync();

            return (true, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> RejectTargetedRequestAsync(Guid shopUserId, Guid requestId)
        {
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(shopUserId);
            if (shop == null) return (false, "Only shop accounts are allowed to perform this action.");

            var request = await _unitOfWork.CommissionRequests.GetByIdAsync(requestId);
            if (request == null) return (false, "The request does not exist.");

            // Kiểm tra xem yêu cầu này có thực sự gửi cho shop này không
            if (request.TargetedShopId != shop.Id || request.Status != CommissionStatus.PendingTarget)
            {
                return (false, "You cannot decline this request.");
            }

            // Đổi trạng thái báo cho khách biết Shop đã từ chối
            request.Status = CommissionStatus.RejectedByShop;

            await _unitOfWork.CommissionRequests.UpdateAsync(request);
            await _unitOfWork.CommitAsync();

            return (true, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> PublishToPoolAsync(Guid userId, Guid requestId)
        {
            var request = await _unitOfWork.CommissionRequests.GetByIdAsync(requestId);
            if (request == null) return (false, "The request does not exist.");

            if (request.UserId != userId) return (false, "You do not have permission to perform this action.");

            // Chỉ cho phép đẩy lên chợ nếu Shop đã từ chối, hoặc ngay từ đầu đang chờ Shop nhưng khách đổi ý
            if (request.Status != CommissionStatus.RejectedByShop &&
                request.Status != CommissionStatus.PendingTarget &&
                request.Status != CommissionStatus.Draft)
            {
                return (false, "You can only publish this request to the public board if it has been rejected by the shop or is pending approval.");
            }

            if (request.Status == CommissionStatus.Draft)
            {
                if (request.TargetedShopId.HasValue)
                {
                    request.Status = CommissionStatus.PendingTarget;
                }
                else
                {
                    request.Status = CommissionStatus.OpenPool;
                }
            }
            else
            {
                // Xóa Target và đẩy lên Pool
                request.TargetedShopId = null;
                request.Status = CommissionStatus.OpenPool;
            }

            if (!request.ShopResponseDeadlineAt.HasValue)
            {
                var responseHours = request.ShopResponseWindowHours > 0
                    ? request.ShopResponseWindowHours
                    : _commissionSettings.DefaultShopResponseHours;
                request.ShopResponseDeadlineAt = DateTime.UtcNow.AddHours(responseHours);
            }

            await _unitOfWork.CommissionRequests.UpdateAsync(request);
            await _unitOfWork.CommitAsync();

            return (true, string.Empty);
        }
        // ======================================================================================
        // TẠO ORDER TỪ BÁO GIÁ 
        // ======================================================================================
        public async Task<(bool Success, Guid? OrderId, string ErrorMessage)> AcceptQuoteAsync(Guid userId, Guid quoteId)
        {
            var quote = await _unitOfWork.CommissionQuotes.GetByIdAsync(quoteId);
            if (quote == null) return (false, null, "Quotation not found.");

            if (quote.ExpiredAt < DateTime.UtcNow)
                return (false, null, "This quotation has expired and cannot be finalized.");

            var request = await _unitOfWork.CommissionRequests.GetByIdAsync(quote.CommissionRequestId);
            if (request == null) return (false, null, "Commission request not found.");

            if (request.UserId != userId) return (false, null, "You do not have permission to finalize this quotation.");

            if (request.Status == CommissionStatus.Completed)
                return (false, null, "This request has already been finalized with another shop.");

            if (quote.Status != QuoteStatus.PendingUserDecision)
                return (false, null, "This quotation is no longer available for acceptance.");

            // 1. Cập nhật statuses
            quote.Status = QuoteStatus.Accepted;
            request.Status = CommissionStatus.Completed;

            foreach (var otherQuote in request.Quotes.Where(q => q.Id != quoteId))
                otherQuote.Status = QuoteStatus.Rejected;

            // 2. Gói toàn bộ dữ liệu Commission vào JSON
            var shopName = quote.Shop?.ShopName ?? "N/A";
            string firstImage = !string.IsNullOrEmpty(request.ReferenceImages)
                ? request.ReferenceImages.Split(',')[0].Trim()
                : "";

            var commissionConfig = new
            {
                Type = "Commission",
                RequestId = request.Id,
                QuoteId = quote.Id,
                ShopId = quote.ShopId,
                ShopName = shopName,
                Title = $"Custom Request: {request.Title}",
                Image = firstImage,
                Price = quote.QuotedPrice,
                Description = request.Description,
                Notes = quote.ShopNotes
            };

            // 3. Tìm hoặc tạo Giỏ hàng (Cart)
            var cart = await _unitOfWork.Carts.GetCartByUserIdAsync(userId);
            if (cart == null)
            {
                cart = new Cart { CustomerId = userId, CreatedAt = DateTime.UtcNow };
                await _unitOfWork.Carts.AddAsync(cart);
                await _unitOfWork.CommitAsync();
            }

            // 4. Thêm item vào CartItem
            var cartItem = new CartItem
            {
                CartId = cart.Id,
                ProductId = null, // Commission không trỏ tới Model cứng
                AssembledProductId = null,
                Quantity = request.Quantity,
                IsCustom = true,
                DesignConfig = JsonSerializer.Serialize(commissionConfig)
            };

            await _unitOfWork.CartItems.AddAsync(cartItem);
            await _unitOfWork.CommitAsync();

            return (true, cart.Id, string.Empty);
        }      

        public async Task<(bool Success, string ErrorMessage)> RevokeQuoteAsync(Guid shopUserId, Guid quoteId)
        {
            var quote = await _unitOfWork.CommissionQuotes.GetByIdAsync(quoteId);
            if (quote == null) return (false, "Quotation does not exist.");

            var shop = await _unitOfWork.Shops.GetByUserIdAsync(shopUserId);
            if (shop == null || quote.ShopId != shop.Id) return (false, "You do not have permission to withdraw this quotation.");

            if (quote.Status != QuoteStatus.PendingUserDecision)
            {
                return (false, "Cannot withdraw a finalized quotation.");
            }

            // Đổi trạng thái thành Revoked
            quote.Status = QuoteStatus.Revoked;
            await _unitOfWork.CommissionQuotes.UpdateAsync(quote);

            // Nếu request không còn báo giá nào (Pending), trả nó về lại chợ chung
            var request = quote.CommissionRequest;
            var activeQuotes = request.Quotes.Count(q => q.Status == QuoteStatus.PendingUserDecision && q.Id != quoteId);
            if (activeQuotes == 0 && request.Status == CommissionStatus.Quoted)
            {
                request.Status = CommissionStatus.OpenPool;
                await _unitOfWork.CommissionRequests.UpdateAsync(request);
            }

            await _unitOfWork.CommitAsync();

            return (true, string.Empty);
        }

        public async Task<(bool Success, string ErrorMessage)> CancelRequestAsync(Guid userId, Guid requestId)
        {
            var request = await _unitOfWork.CommissionRequests.GetByIdAsync(requestId);
            if (request == null) return (false, "The request does not exist.");

            if (request.UserId != userId) return (false, "You do not have permission to perform actions on this request.");

            // Chỉ cho phép hủy nếu chưa chốt đơn
            if (request.Status == CommissionStatus.Completed)
            {
                return (false, "This request cannot be canceled because it has already been finalized and converted into an order.");
            }
            if (request.Status == CommissionStatus.Canceled)
            {
                return (false, "This request has already been canceled previously.");
            }

            request.Status = CommissionStatus.Canceled;

            // Tùy chọn: Đổi trạng thái các Quote bên trong thành Rejected luôn
            foreach (var quote in request.Quotes)
            {
                quote.Status = QuoteStatus.Rejected;
                await _unitOfWork.CommissionQuotes.UpdateAsync(quote);
            }

            await _unitOfWork.CommissionRequests.UpdateAsync(request);
            await _unitOfWork.CommitAsync();

            return (true, string.Empty);
        }
        public async Task<(bool Success, string ErrorMessage)> UpdateQuoteAsync(Guid shopUserId, Guid quoteId, SubmitQuoteRequest updateDto)
        {
            var quote = await _unitOfWork.CommissionQuotes.GetByIdAsync(quoteId);
            if (quote == null) return (false, "The request does not exist.");

            // Kiểm tra quyền của Shop
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(shopUserId);
            if (shop == null || quote.ShopId != shop.Id) return (false, "You do not have permission to perform actions on this request.");

            return (false, "Updating a quotation is not allowed. Please revoke and submit a new quotation.");
        }

        public async Task<IEnumerable<CommissionRequestResponse>> GetShopTargetedRequestsAsync(Guid shopUserId)
        {
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(shopUserId);
            if (shop == null) return new List<CommissionRequestResponse>();

            var requests = await _unitOfWork.CommissionRequests.GetTargetedRequestsForShopAsync(shop.Id);
            return _mapper.Map<IEnumerable<CommissionRequestResponse>>(requests);
        }

        public async Task<IEnumerable<CommissionQuoteResponse>> GetShopQuotesAsync(Guid shopUserId)
        {
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(shopUserId);
            if (shop == null) return new List<CommissionQuoteResponse>();

            var quotes = await _unitOfWork.CommissionQuotes.GetQuotesByShopIdAsync(shop.Id);
            return _mapper.Map<IEnumerable<CommissionQuoteResponse>>(quotes);
        }

        public async Task ProcessCommissionRemindersAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var reminderInterval = TimeSpan.FromHours(_commissionSettings.ReminderIntervalHours);
            var maxReminders = _commissionSettings.MaxReminderCount;

            var expiredShopRequests = await _unitOfWork.CommissionRequests.GetExpiredShopResponseRequestsAsync(now);
            foreach (var request in expiredShopRequests)
            {
                if (request.Status != CommissionStatus.PendingTarget || !request.ShopResponseDeadlineAt.HasValue)
                {
                    continue;
                }

                bool reminderDue = request.ReminderCount < maxReminders
                    && (request.LastReminderAt == null || now - request.LastReminderAt.Value >= reminderInterval);

                if (reminderDue && request.TargetedShop != null)
                {
                    request.ReminderCount += 1;
                    request.LastReminderAt = now;
                    await _unitOfWork.CommissionRequests.UpdateAsync(request);

                    await _notificationService.SendNotificationAsync(
                        request.TargetedShop.UserId,
                        "Commission request needs a response",
                        $"Please respond to the commission request \"{request.Title}\".",
                        "System",
                        referenceId: request.Id.ToString(),
                        referenceType: "CommissionRequest",
                        redirectUrl: $"/shop/commission/requests/{request.Id}",
                        actorId: request.UserId);

                    continue;
                }

                bool autoRejectDue = request.ReminderCount >= maxReminders
                    && request.LastReminderAt.HasValue
                    && now - request.LastReminderAt.Value >= reminderInterval;

                if (autoRejectDue)
                {
                    request.Status = CommissionStatus.RejectedByShop;
                    await _unitOfWork.CommissionRequests.UpdateAsync(request);

                    await _notificationService.SendNotificationAsync(
                        request.UserId,
                        "Commission request timed out",
                        "The targeted shop did not respond in time. You can publish the request to the public board.",
                        "System",
                        referenceId: request.Id.ToString(),
                        referenceType: "CommissionRequest",
                        redirectUrl: $"/commissions/{request.Id}");
                }
            }

            var expiredQuotes = await _unitOfWork.CommissionQuotes.GetExpiredCustomerDecisionQuotesAsync(now);
            var processedRequests = new HashSet<Guid>();

            foreach (var quote in expiredQuotes)
            {
                if (quote.Status != QuoteStatus.PendingUserDecision)
                {
                    continue;
                }

                var request = quote.CommissionRequest;
                if (request == null || request.Status == CommissionStatus.Completed || request.Status == CommissionStatus.Canceled)
                {
                    continue;
                }

                bool reminderDue = quote.CustomerReminderCount < maxReminders
                    && (quote.LastCustomerReminderAt == null || now - quote.LastCustomerReminderAt.Value >= reminderInterval);

                if (reminderDue)
                {
                    quote.CustomerReminderCount += 1;
                    quote.LastCustomerReminderAt = now;
                    await _unitOfWork.CommissionQuotes.UpdateAsync(quote);

                    var shopName = quote.Shop?.ShopName ?? "a shop";
                    await _notificationService.SendNotificationAsync(
                        request.UserId,
                        "Commission quotation awaiting your response",
                        $"Please review the quotation from {shopName} for \"{request.Title}\".",
                        "System",
                        referenceId: quote.Id.ToString(),
                        referenceType: "CommissionQuote",
                        redirectUrl: $"/commissions/{request.Id}",
                        actorId: quote.Shop?.UserId);

                    continue;
                }

                bool autoCancelDue = quote.CustomerReminderCount >= maxReminders
                    && quote.LastCustomerReminderAt.HasValue
                    && now - quote.LastCustomerReminderAt.Value >= reminderInterval;

                if (!autoCancelDue || !processedRequests.Add(request.Id))
                {
                    continue;
                }

                request.Status = CommissionStatus.Canceled;
                foreach (var pendingQuote in request.Quotes.Where(q => q.Status == QuoteStatus.PendingUserDecision))
                {
                    pendingQuote.Status = QuoteStatus.Rejected;
                }

                var user = await _unitOfWork.Users.GetByIdAsync(request.UserId);
                if (user != null)
                {
                    user.YMonthlyAutoCancels += 1;
                    user.TotalAutoCancels += 1;
                    user.SlowResponseViolationCount += 1;
                    int newScore = await _reputationService.AdjustReputationAsync(
                        ReputationTargetType.Customer,
                        user.Id,
                        -_reputationSettings.PointsDeductNoResponseAfterAccept,
                        $"Commission request #{request.Id} auto-cancelled");
                    user.CurrentReputationScore = newScore;
                    if (user.YMonthlyAutoCancels >= _reputationSettings.MaxMonthlyAutoCancels)
                    {
                        user.Status = AccountStatus.Suspended;
                    }
                    await _unitOfWork.Users.UpdateAsync(user);
                }

                await _unitOfWork.CommissionRequests.UpdateAsync(request);

                await _notificationService.SendNotificationAsync(
                    request.UserId,
                    "Commission request canceled due to no response",
                    $"Your commission request \"{request.Title}\" was canceled because no response was received in time.",
                    "System",
                    referenceId: request.Id.ToString(),
                    referenceType: "CommissionRequest",
                    redirectUrl: $"/commissions/{request.Id}");

                foreach (var pendingQuote in request.Quotes.Where(q => q.Shop != null))
                {
                    await _notificationService.SendNotificationAsync(
                        pendingQuote.Shop.UserId,
                        "Commission request canceled",
                        $"The commission request \"{request.Title}\" was canceled due to no customer response.",
                        "System",
                        referenceId: request.Id.ToString(),
                        referenceType: "CommissionRequest",
                        redirectUrl: $"/shop/commission/requests/{request.Id}");
                }
            }

            await _unitOfWork.CommitAsync();
        }
    }
}
    