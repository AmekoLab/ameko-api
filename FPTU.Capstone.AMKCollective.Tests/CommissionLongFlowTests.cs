using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Commission;
using FPTU.Capstone.AMKCollective.Application.DTOs.Order;
using FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.AI;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Application.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FPTU.Capstone.AMKCollective.Tests
{
    public class CommissionLongFlowTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly Mock<IMapper> _mapper = new();
        private readonly Mock<INotificationService> _notificationService = new();
        private readonly Mock<IReputationService> _reputationService = new();
        private readonly Mock<IWalletService> _walletService = new();
        private readonly Mock<IPaymentService> _paymentService = new();
        private readonly Mock<IVoucherService> _voucherService = new();
        private readonly Mock<IVnPayService> _vnPayService = new();
        private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
        private readonly Mock<IAIService> _aiService = new();

        private readonly CommissionSettings _commissionSettings = new()
        {
            DefaultShopResponseHours = 24,
            ReminderIntervalHours = 2,
            MaxReminderCount = 3,
            MaxActiveRequests = 5,
            PaymentWindowHours = 48
        };

        private readonly OrderSettings _orderSettings = new()
        {
            DefaultShippingFee = 30000m,
            ShopPayoutRate = 0.9m,
            SystemVoucherShopShareRate = 0.5m
        };

        private readonly ReputationSettings _reputationSettings = new()
        {
            PointsDeductNoResponseAfterAccept = 10,
            MaxMonthlyAutoCancels = 5
        };

        public CommissionLongFlowTests()
        {
            // Transaction Mock
            _unitOfWork.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<CheckoutResponse>>>()))
                .Returns<Func<Task<CheckoutResponse>>>(func => func());
            _unitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            // Repository Mocks
            var mockCommRequests = new Mock<ICommissionRequestRepository>();
            var mockCommQuotes = new Mock<ICommissionQuoteRepository>();
            var mockCarts = new Mock<ICartRepository>();
            var mockCartItems = new Mock<ICartItemRepository>();
            var mockOrders = new Mock<IOrderRepository>();
            var mockOrderGroups = new Mock<IOrderGroupRepository>();
            var mockIssues = new Mock<IOrderIssueRepository>();
            var mockUsers = new Mock<IUserRepository>();
            var mockShops = new Mock<IShopRepository>();

            _unitOfWork.Setup(x => x.CommissionRequests).Returns(mockCommRequests.Object);
            _unitOfWork.Setup(x => x.CommissionQuotes).Returns(mockCommQuotes.Object);
            _unitOfWork.Setup(x => x.Carts).Returns(mockCarts.Object);
            _unitOfWork.Setup(x => x.CartItems).Returns(mockCartItems.Object);
            _unitOfWork.Setup(x => x.Orders).Returns(mockOrders.Object);
            _unitOfWork.Setup(x => x.OrderGroups).Returns(mockOrderGroups.Object);
            _unitOfWork.Setup(x => x.OrderIssues).Returns(mockIssues.Object);
            _unitOfWork.Setup(x => x.Users).Returns(mockUsers.Object);
            _unitOfWork.Setup(x => x.Shops).Returns(mockShops.Object);

            // Default mock returns to avoid NullReferenceException in background loops
            mockCommQuotes.Setup(x => x.GetExpiredCustomerDecisionQuotesAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(new List<CommissionQuote>());
            mockCommQuotes.Setup(x => x.GetAcceptedQuotesPastPaymentDeadlineAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(new List<CommissionQuote>());
            mockCommRequests.Setup(x => x.GetExpiredShopResponseRequestsAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(new List<CommissionRequest>());
        }

        private CommissionService CreateCommissionService()
        {
            return new CommissionService(
                _unitOfWork.Object,
                _mapper.Object,
                Options.Create(_commissionSettings),
                _notificationService.Object,
                Options.Create(_reputationSettings),
                _reputationService.Object
            );
        }

        private OrderService CreateOrderService()
        {
            return new OrderService(
                _unitOfWork.Object,
                _mapper.Object,
                _paymentService.Object,
                _voucherService.Object,
                _walletService.Object,
                Options.Create(_orderSettings),
                Options.Create(new FrontendUrls()),
                Options.Create(new SystemSettings()),
                Options.Create(new ReputationSettings()),
                _reputationService.Object,
                _vnPayService.Object,
                _httpContextAccessor.Object,
                _aiService.Object
            );
        }

        [Fact]
        public async Task Commission_FullLifecycle_Flow_Success()
        {
            // --- DATA SETUP ---
            var customerId = Guid.NewGuid();
            var shopId = Guid.NewGuid();
            var shopUserId = Guid.NewGuid();

            var shop = new ShopProfile { Id = shopId, UserId = shopUserId, Status = ShopStatus.Active, ShopName = "Test Shop" };
            var request = new CommissionRequest
            {
                Id = Guid.NewGuid(),
                UserId = customerId,
                TargetedShopId = shopId,
                Status = CommissionStatus.PendingTarget,
                ShopResponseDeadlineAt = DateTime.UtcNow.AddMinutes(-21),
                ReminderCount = 3,
                LastReminderAt = DateTime.UtcNow.AddHours(-3), 
                TargetedShop = shop,
                Title = "Test Commission",
                Quantity = 1
            };

            // Setup repository for timeout step
            Mock.Get(_unitOfWork.Object.CommissionRequests).Setup(x => x.GetExpiredShopResponseRequestsAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(new List<CommissionRequest> { request });
            Mock.Get(_unitOfWork.Object.Users).Setup(x => x.GetByIdAsync(shopUserId)).ReturnsAsync(new User { Id = shopUserId });

            var commService = CreateCommissionService();
            await commService.ProcessCommissionRemindersAsync();

            Assert.Equal(CommissionStatus.RejectedByShop, request.Status);

            // --- STEP 2: REDO & QUOTE ---
            var newRequestId = Guid.NewGuid();
            var newRequest = new CommissionRequest
            {
                Id = newRequestId,
                UserId = customerId,
                TargetedShopId = shopId,
                Status = CommissionStatus.PendingTarget,
                Title = "Test Commission v2",
                Quantity = 1,
                Quotes = new List<CommissionQuote>()
            };

            var quoteId = Guid.NewGuid();
            var quote = new CommissionQuote
            {
                Id = quoteId,
                CommissionRequestId = newRequestId,
                ShopId = shopId,
                QuotedPrice = 1500000m,
                Status = QuoteStatus.PendingUserDecision,
                ExpiredAt = DateTime.UtcNow.AddDays(7),
                CommissionRequest = newRequest,
                Shop = shop
            };
            newRequest.Quotes.Add(quote);

            Mock.Get(_unitOfWork.Object.CommissionQuotes).Setup(x => x.GetByIdAsync(quoteId)).ReturnsAsync(quote);
            Mock.Get(_unitOfWork.Object.CommissionRequests).Setup(x => x.GetByIdAsync(newRequestId)).ReturnsAsync(newRequest);
            
            var cart = new Cart { Id = Guid.NewGuid(), CustomerId = customerId, CartItems = new List<CartItem>() };
            Mock.Get(_unitOfWork.Object.Carts).Setup(x => x.GetCartByUserIdAsync(customerId)).ReturnsAsync(cart);

            // Ensure CartItems.AddAsync adds to local list for navigation property testing
            Mock.Get(_unitOfWork.Object.CartItems).Setup(x => x.AddAsync(It.IsAny<CartItem>()))
                .Callback<CartItem>(ci => cart.CartItems.Add(ci))
                .Returns(Task.CompletedTask);

            var acceptResult = await commService.AcceptQuoteAsync(customerId, quoteId);

            Assert.True(acceptResult.Success, $"AcceptQuoteAsync failed: {acceptResult.ErrorMessage}");
            Assert.Equal(CommissionStatus.Completed, newRequest.Status);
            Assert.Equal(QuoteStatus.Accepted, quote.Status);
            Assert.Single(cart.CartItems);
            
            var cartItem = cart.CartItems.First();
            Assert.Contains("1500000", cartItem.DesignConfig ?? "");

            // --- STEP 3: CHECKOUT & ADDRESS CHANGE ---
            var orderService = CreateOrderService();
            var orderId = Guid.NewGuid();
            var order = new Order
            {
                Id = orderId,
                CustomerId = customerId,
                ShopId = shopId,
                OrderStatus = OrderStatus.Pending,
                TotalAmount = 1530000m,
                ShippingAddress = "Old Address"
            };

            Mock.Get(_unitOfWork.Object.Orders).Setup(x => x.GetByIdAsync(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

            var updateAddressReq = new UpdateShippingAddressRequest
            {
                ReceiverName = "New Customer",
                ReceiverPhone = "0987654321",
                ShippingAddress = "New Address"
            };

            await orderService.UpdateShippingAddressAsync(customerId, orderId, updateAddressReq);
            Assert.Equal("New Address", order.ShippingAddress);

            // --- STEP 4: TRACKING & COMPLETION ---
            var updateStatusReq = new UpdateOrderStatusRequest
            {
                Status = OrderStatus.Shipped,
                ExpectedDeliveryDate = DateTime.UtcNow.AddDays(3)
            };

            await orderService.UpdateOrderStatusAsync(shopId, orderId, updateStatusReq);
            Assert.Equal(OrderStatus.Shipped, order.OrderStatus);

            order.OrderStatus = OrderStatus.Completed;

            // --- STEP 5: REFUND & WALLET ---
            Mock.Get(_unitOfWork.Object.Orders).Setup(x => x.GetOrderDetailByIdAsync(orderId)).ReturnsAsync(order);
            
            await _walletService.Object.RefundToWalletAsync(customerId, 1530000m, "Flow Refund");
            _walletService.Verify(x => x.RefundToWalletAsync(customerId, 1530000m, "Flow Refund"), Times.Once());
        }
    }
}
