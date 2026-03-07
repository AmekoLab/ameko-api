using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs.Commission;
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
using System.Threading.Tasks;
using System.Text.Json;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class CommissionService : ICommissionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly CommissionSettings _commissionSettings;
        public CommissionService(IUnitOfWork unitOfWork, IMapper mapper, IOptions<CommissionSettings> commissionSettings)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _commissionSettings = commissionSettings.Value;
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
            if (request.UserId != currentUserId)
            {

                var shop = await _unitOfWork.Shops.GetByUserIdAsync(currentUserId);

                if (shop != null)
                {
                    response.Quotes = response.Quotes.Where(q => q.ShopId == shop.Id).ToList();
                }
                else
                {

                    response.Quotes.Clear();
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

        public async Task<Guid> CreateRequestAsync(Guid userId, CreateCommissionRequest requestDto)
        {
            var request = _mapper.Map<CommissionRequest>(requestDto);
            request.UserId = userId;

            // Có chỉ định thì chờ shop không thì ném lên chợ chung
            request.Status = requestDto.TargetedShopId.HasValue
                ? CommissionStatus.PendingTarget
                : CommissionStatus.OpenPool;

            await _unitOfWork.CommissionRequests.AddAsync(request);
            await _unitOfWork.CommitAsync();

            return request.Id;
        }

        public async Task<(bool Success, string ErrorMessage)> SubmitQuoteAsync(Guid shopUserId, Guid requestId, SubmitQuoteRequest quoteDto)
        {
            // Lấy profile Shop của user đang đăng nhập
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(shopUserId);
            if (shop == null) return (false, "Only shop accounts are allowed to submit quotations.");

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

            // Check shop đã báo giá chưa (tránh báo giá trùng)
            if (request.Quotes.Any(q => q.ShopId == shop.Id))
            {
                return (false, "Your shop has already submitted a quotation for this request.");
            }

            var quote = _mapper.Map<CommissionQuote>(quoteDto);
            quote.CommissionRequestId = requestId;
            quote.ShopId = shop.Id;
            quote.Status = QuoteStatus.PendingUserDecision;
            quote.ExpiredAt = DateTime.UtcNow.AddDays(_commissionSettings.QuoteValidityDays);

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
            if (request.Status != CommissionStatus.RejectedByShop && request.Status != CommissionStatus.PendingTarget)
            {
                return (false, "You can only publish this request to the public board if it has been rejected by the shop or is pending approval.");
            }

            // Xóa Target và đẩy lên Pool
            request.TargetedShopId = null;
            request.Status = CommissionStatus.OpenPool;

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

            // Load request via CommissionRequests to get ALL quotes fully populated
            var request = await _unitOfWork.CommissionRequests.GetByIdAsync(quote.CommissionRequestId);
            if (request == null) return (false, null, "Commission request not found.");

            if (request.UserId != userId) return (false, null, "You do not have permission to finalize this quotation.");

            if (request.Status == CommissionStatus.Completed)
                return (false, null, "This request has already been finalized with another shop.");

            // 1. Cập nhật commission statuses — EF change tracking handles these automatically
            quote.Status = QuoteStatus.Accepted;
            request.Status = CommissionStatus.Completed;

            foreach (var otherQuote in request.Quotes.Where(q => q.Id != quoteId))
                otherQuote.Status = QuoteStatus.Rejected;

            // 2. Build OrderItem data — reuse quote.Shop (already tracked) to avoid loading
            //    extra tracked entities that can interfere with the change tracker
            var shopName = quote.Shop?.ShopName ?? "N/A";
            decimal totalPrice = quote.QuotedPrice * request.Quantity;
            string firstImage = !string.IsNullOrEmpty(request.ReferenceImages)
                ? request.ReferenceImages.Split(',')[0].Trim()
                : "";

            var commissionConfig = new
            {
                Type = "Commission",
                ShopId = quote.ShopId,
                ShopName = shopName,
                Description = request.Description
            };

            // 3. Load cart with AsNoTracking to avoid tracking conflicts with the commission entities
            //    above (same pattern used in AddToCartAsync)
            var existingCart = await _unitOfWork.Orders.GetCartOnlyAsync(userId);

            if (existingCart == null)
            {
                // No cart yet — INSERT new Order + OrderItem together
                var newCart = new Order
                {
                    Id = Guid.NewGuid(),
                    CustomerId = userId,
                    OrderStatus = OrderStatus.InCart,
                    PaymentStatus = PaymentStatus.Pending,
                    SubTotal = totalPrice,
                    TotalAmount = totalPrice,
                };
                var orderItem = new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = newCart.Id,
                    ProductId = null,
                    AssembledProductId = null,
                    ProductName = $"Custom Request: {request.Title}",
                    ProductImage = firstImage,
                    Quantity = request.Quantity,
                    UnitPrice = quote.QuotedPrice,
                    TotalPrice = totalPrice,
                    IsCustom = true,
                    DesignConfig = JsonSerializer.Serialize(commissionConfig),
                    Notes = quote.ShopNotes
                };
                newCart.OrderItems.Add(orderItem);
                await _unitOfWork.Orders.AddAsync(newCart);

                await _unitOfWork.CommitAsync();
                return (true, newCart.Id, string.Empty);
            }
            else
            {
                // Cart exists — INSERT new OrderItem + UPDATE totals via stub (no tracked Order entity)
                var orderItem = new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = existingCart.Id,
                    ProductId = null,
                    AssembledProductId = null,
                    ProductName = $"Custom Request: {request.Title}",
                    ProductImage = firstImage,
                    Quantity = request.Quantity,
                    UnitPrice = quote.QuotedPrice,
                    TotalPrice = totalPrice,
                    IsCustom = true,
                    DesignConfig = JsonSerializer.Serialize(commissionConfig),
                    Notes = quote.ShopNotes
                };
                await _unitOfWork.Orders.AddOrderItemAsync(orderItem);

                // GetCartOnlyAsync already filtered IsDeleted, so sum directly
                decimal newTotal = existingCart.OrderItems.Sum(i => i.TotalPrice) + totalPrice;
                _unitOfWork.Orders.UpdateCartTotal(existingCart.Id, newTotal);

                await _unitOfWork.CommitAsync();
                return (true, existingCart.Id, string.Empty);
            }
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

            // Chỉ cho sửa nếu khách chưa duyệt
            if (quote.Status != QuoteStatus.PendingUserDecision)
            {
                return (false, "You cannot modify the quotation once the customer has made their decision.");
            }

            // Cập nhật thông tin
            quote.QuotedPrice = updateDto.QuotedPrice;
            quote.EstimatedDays = updateDto.EstimatedDays;
            quote.ShopNotes = updateDto.ShopNotes;

            await _unitOfWork.CommissionQuotes.UpdateAsync(quote);
            await _unitOfWork.CommitAsync();

            return (true, string.Empty);
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
    }
}
    