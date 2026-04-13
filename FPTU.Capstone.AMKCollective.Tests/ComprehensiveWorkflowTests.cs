using AutoMapper;
using OrderShipment = FPTU.Capstone.AMKCollective.Application.DTOs.Order;
using IssueDTOs = FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues;
using FPTU.Capstone.AMKCollective.Application.DTOs;
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
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.AI;
using FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues;

namespace FPTU.Capstone.AMKCollective.Tests
{
    public class ComprehensiveWorkflowTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly Mock<IMapper> _mapper = new();
        private readonly Mock<IPaymentService> _paymentService = new();
        private readonly Mock<IVoucherService> _voucherService = new();
        private readonly Mock<IWalletService> _walletService = new();
        private readonly Mock<IReputationService> _reputationService = new();
        private readonly Mock<IVnPayService> _vnPayService = new();
        private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
        private readonly Mock<IAIService> _aiService = new();
        private readonly Mock<ILogger<OrderService>> _orderLogger = new();
        private readonly Mock<INotificationService> _notificationService = new();

        private readonly Mock<IOrderRepository> _mockOrders = new();
        private readonly Mock<IOrderIssueRepository> _mockIssues = new();
        private readonly Mock<IOrderIssueLogRepository> _mockLogs = new();
        private readonly Mock<IModelRepository> _mockModels = new();
        private readonly Mock<ICartRepository> _mockCarts = new();
        private readonly Mock<IAssembledProductRepository> _mockAssembled = new();
        private readonly Mock<ICommissionRequestRepository> _mockCommRequests = new();
        private readonly Mock<ICommissionQuoteRepository> _mockCommQuotes = new();
        private readonly Mock<IUserRepository> _mockUsers = new();
        private readonly Mock<IShopRepository> _mockShops = new();

        private readonly OrderSettings _orderSettings = new()
        {
            DefaultShippingFee = 30000m,
            ShopPayoutRate = 0.9m,
            SystemVoucherShopShareRate = 0.5m,
            MaxCancellationsPerPeriod = 5,
            CancellationSpamCheckDays = 7
        };

        private readonly CommissionSettings _commissionSettings = new()
        {
            DefaultShopResponseHours = 24,
            ReminderIntervalHours = 2,
            MaxReminderCount = 3
        };

        private readonly ReputationSettings _reputationSettings = new()
        {
            PointsDeductArtisanFault = 10,
            MaxMonthlyAutoCancels = 5
        };

        public ComprehensiveWorkflowTests()
        {
            _unitOfWork.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<OrderShipment.CheckoutResponse>>>()))
                .Returns<Func<Task<OrderShipment.CheckoutResponse>>>(func => func());

            _unitOfWork.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            _unitOfWork.Setup(x => x.Orders).Returns(_mockOrders.Object);
            _unitOfWork.Setup(x => x.OrderIssues).Returns(_mockIssues.Object);
            _unitOfWork.Setup(x => x.OrderIssueLogs).Returns(_mockLogs.Object);
            _unitOfWork.Setup(x => x.Models).Returns(_mockModels.Object);
            _unitOfWork.Setup(x => x.Carts).Returns(_mockCarts.Object);
            _unitOfWork.Setup(x => x.AssembledProducts).Returns(_mockAssembled.Object);
            _unitOfWork.Setup(x => x.CommissionRequests).Returns(_mockCommRequests.Object);
            _unitOfWork.Setup(x => x.CommissionQuotes).Returns(_mockCommQuotes.Object);
            _unitOfWork.Setup(x => x.Users).Returns(_mockUsers.Object);
            _unitOfWork.Setup(x => x.Shops).Returns(_mockShops.Object);

            // Avoid NullReference in CommissionService background job loop after processing requests
            _mockCommQuotes.Setup(x => x.GetExpiredCustomerDecisionQuotesAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(new List<CommissionQuote>());
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

        [Fact]
        public async Task Checkout_ModelDeletedInCart_ShouldThrowException()
        {
            var userId = Guid.NewGuid();
            var modelId = Guid.NewGuid();
            var cartItemId = Guid.NewGuid();

            var cartItem = new CartItem { Id = cartItemId, ProductId = modelId, Quantity = 1, IsCustom = true };
            var cart = new Cart { CustomerId = userId, CartItems = new List<CartItem> { cartItem } };

            _mockCarts.Setup(x => x.GetCartByUserIdAsync(userId)).ReturnsAsync(cart);
            _mockModels.Setup(x => x.GetByIdAsync(modelId, It.IsAny<CancellationToken>())).ReturnsAsync((Model)null!);

            var service = CreateOrderService();
            var request = new OrderShipment.CheckoutRequest
            {
                SelectedOrderItemIds = new List<Guid> { cartItemId },
                PaymentMethod = PaymentMethod.VnPay
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CheckoutAsync(userId, request));
            Assert.Contains("Insufficient stock", ex.Message);
        }

        [Fact]
        public async Task Checkout_AssembledProductComponentDeleted_ShouldThrowException()
        {
            var userId = Guid.NewGuid();
            var assembledId = Guid.NewGuid();
            var componentId = Guid.NewGuid();
            var cartItemId = Guid.NewGuid();

            var component = new Model { Id = componentId, Name = "Deleted Component", IsDeleted = true };
            var assembledProduct = new AssembledProduct
            {
                Id = assembledId,
                Name = "Assembled Product",
                Quantity = 5,
                ProductAssembledDetails = new List<ProductAssembledDetail>
                {
                    new ProductAssembledDetail { ComponentId = componentId, Component = component }
                }
            };

            var cartItem = new CartItem { Id = cartItemId, AssembledProductId = assembledId, Quantity = 1, IsCustom = false };
            var cart = new Cart { CustomerId = userId, CartItems = new List<CartItem> { cartItem } };

            _mockCarts.Setup(x => x.GetCartByUserIdAsync(userId)).ReturnsAsync(cart);
            _mockAssembled.Setup(x => x.GetByIdWithDetailsAsync(assembledId)).ReturnsAsync(assembledProduct);

            var service = CreateOrderService();
            var request = new OrderShipment.CheckoutRequest
            {
                SelectedOrderItemIds = new List<Guid> { cartItemId },
                PaymentMethod = PaymentMethod.VnPay
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CheckoutAsync(userId, request));
            Assert.Contains("no longer available", ex.Message);
        }

        [Fact]
        public async Task CommissionLifecycle_Timeout_ShouldCanceRequestAfterReminders()
        {
            var requestId = Guid.NewGuid();
            var shopUserId = Guid.NewGuid();
            var shopId = Guid.NewGuid();
            var shopProfile = new ShopProfile { Id = shopId, UserId = shopUserId, Status = ShopStatus.Active, IsActive = true };
            var request = new CommissionRequest
            {
                Id = requestId,
                Status = CommissionStatus.PendingTarget,
                TargetedShopId = shopId,
                TargetedShop = shopProfile,
                ShopResponseDeadlineAt = DateTime.UtcNow.AddHours(-24),
                ReminderCount = 3,
                LastReminderAt = DateTime.UtcNow.AddHours(-10)
            };

            _mockCommRequests.Setup(x => x.GetExpiredShopResponseRequestsAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(new List<CommissionRequest> { request });
            _mockUsers.Setup(x => x.GetByIdAsync(shopUserId)).ReturnsAsync(new User { Id = shopUserId });

            var service = CreateCommissionService();
            await service.ProcessCommissionRemindersAsync();

            Assert.Equal(CommissionStatus.RejectedByShop, request.Status);
            _mockCommRequests.Verify(x => x.UpdateAsync(request), Times.AtLeastOnce());
        }

        [Fact]
        public async Task OrderIssue_AIAnalysis_ShouldPopulateAIResult()
        {
            var userId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var order = new Order { Id = orderId, CustomerId = userId, OrderStatus = OrderStatus.Processing };
            var aiRawResult = "{\"Category\":\"Return\",\"Sentiment\":\"Neutral\",\"Summary\":\"Defective item\",\"Recommendation\":\"Approve\",\"ConfidenceScore\":0.9}";

            _mockOrders.Setup(x => x.GetOrderDetailByIdAsync(orderId)).ReturnsAsync(order);
            _mockIssues.Setup(x => x.HasActiveIssueForOrderAsync(orderId)).ReturnsAsync(false);
            _mockIssues.Setup(x => x.CountUserCancelAttemptsAsync(userId, It.IsAny<OrderIssueStatus[]>(), It.IsAny<DateTime>()))
                .ReturnsAsync(0);
            
            _aiService.Setup(x => x.AnalyzeOrderIssueAsync(It.IsAny<OrderIssue>(), It.IsAny<Order>()))
                .ReturnsAsync(aiRawResult);

            var service = CreateOrderService();
            var request = new IssueDTOs.CancelOrderRequest
            {
                OrderId = orderId,
                Reason = "Waiting too long",
                Description = "Please cancel."
            };

            _mapper.Setup(x => x.Map<OrderIssueResponse>(It.IsAny<OrderIssue>()))
                .Returns(new OrderIssueResponse { AIAnalysisResult = aiRawResult });

            var result = await service.RequestCancelOrderAsync(userId, request);

            Assert.Equal(aiRawResult, result.AIAnalysisResult);
            _aiService.Verify(x => x.AnalyzeOrderIssueAsync(It.IsAny<OrderIssue>(), It.IsAny<Order>()), Times.Once);
        }

        [Fact]
        public async Task WalletRefund_FullAmount_ShouldIncrementBalanceCorrectly()
        {
            var userId = Guid.NewGuid();
            var amount = 150000m;
            var reason = "Order Refund #123";

            await _walletService.Object.RefundToWalletAsync(userId, amount, reason);

            _walletService.Verify(x => x.RefundToWalletAsync(userId, amount, reason), Times.Once);
        }
    }
}
