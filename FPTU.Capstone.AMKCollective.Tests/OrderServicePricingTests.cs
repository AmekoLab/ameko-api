using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs.Builder;
using FPTU.Capstone.AMKCollective.Application.DTOs.Order;
using FPTU.Capstone.AMKCollective.Application.DTOs.Payment;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Application.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using Xunit;

namespace FPTU.Capstone.AMKCollective.Tests
{
    public class OrderServicePricingTests
    {
        [Fact]
        public async Task GetMyCartAsync_ComputesRealtimePriceForCustomAndAssembledItems()
        {
            var userId = Guid.NewGuid();
            var customItemId = Guid.NewGuid();
            var assembledItemId = Guid.NewGuid();
            var baseKitId = Guid.NewGuid();
            var partId = Guid.NewGuid();
            var assembledId = Guid.NewGuid();
            var assembledShopModelId = Guid.NewGuid();
            var customShopId = Guid.NewGuid();
            var assembledShopId = Guid.NewGuid();

            var selectedParts = new Dictionary<string, SelectedPartResponse>
            {
                ["switch"] = new SelectedPartResponse
                {
                    Id = partId,
                    Name = "Switch",
                    Price = 20m,
                    Quantity = 2,
                    ThumbnailUrl = "switch.png"
                }
            };

            var designConfig = JsonSerializer.Serialize(new
            {
                SessionId = Guid.NewGuid(),
                BaseKitId = baseKitId,
                SelectedItemsJson = JsonSerializer.Serialize(selectedParts)
            });

            var cart = new Cart
            {
                Id = Guid.NewGuid(),
                CustomerId = userId,
                CreatedAt = DateTime.UtcNow,
                CartItems = new List<CartItem>
                {
                    new()
                    {
                        Id = customItemId,
                        ProductId = baseKitId,
                        Quantity = 2,
                        IsCustom = true,
                        DesignConfig = designConfig
                    },
                    new()
                    {
                        Id = assembledItemId,
                        AssembledProductId = assembledId,
                        Quantity = 3,
                        IsCustom = false
                    }
                }
            };

            var baseKit = new Model { Id = baseKitId, Name = "Base Kit", Price = 100m, ShopId = customShopId, ThumbnailURL = "base.png" };
            var assembledShopModel = new Model { Id = assembledShopModelId, Name = "Shop Model", Price = 1m, ShopId = assembledShopId };
            var assembled = new AssembledProduct
            {
                Id = assembledId,
                Name = "Assembled 75",
                Price = 300m,
                Image1 = "assembled.png",
                ProductAssembledDetails = new List<ProductAssembledDetail>
                {
                    new()
                    {
                        BaseKitId = assembledShopModelId,
                        ComponentId = Guid.Empty,
                        Quantity = 1
                    }
                }
            };

            var cartsRepo = new Mock<ICartRepository>();
            cartsRepo.Setup(x => x.GetCartByUserIdAsync(userId)).ReturnsAsync(cart);

            var modelRepo = new Mock<IModelRepository>();
            modelRepo.Setup(x => x.GetByIdAsync(baseKitId, It.IsAny<CancellationToken>())).ReturnsAsync(baseKit);
            modelRepo.Setup(x => x.GetByIdAsync(assembledShopModelId, It.IsAny<CancellationToken>())).ReturnsAsync(assembledShopModel);

            var assembledRepo = new Mock<IAssembledProductRepository>();
            assembledRepo.Setup(x => x.GetByIdWithDetailsAsync(assembledId)).ReturnsAsync(assembled);

            var shopRepo = new Mock<IShopRepository>();
            shopRepo.Setup(x => x.GetByIdAsync(customShopId, It.IsAny<CancellationToken>())).ReturnsAsync(new ShopProfile { Id = customShopId, ShopName = "Custom Shop" });
            shopRepo.Setup(x => x.GetByIdAsync(assembledShopId, It.IsAny<CancellationToken>())).ReturnsAsync(new ShopProfile { Id = assembledShopId, ShopName = "Assembled Shop" });

            var uow = new Mock<IUnitOfWork>();
            uow.SetupGet(x => x.Carts).Returns(cartsRepo.Object);
            uow.SetupGet(x => x.Models).Returns(modelRepo.Object);
            uow.SetupGet(x => x.AssembledProducts).Returns(assembledRepo.Object);
            uow.SetupGet(x => x.Shops).Returns(shopRepo.Object);

            var service = CreateOrderService(uow);

            var result = await service.GetMyCartAsync(userId);

            Assert.NotNull(result);
            Assert.Equal(2, result!.OrderItems.Count);

            var custom = result.OrderItems.First(x => x.OrderItemId == customItemId);
            Assert.True(custom.IsCustom);
            Assert.Equal(140m, custom.UnitPrice);
            Assert.Equal(280m, custom.TotalPrice);

            var assembledItem = result.OrderItems.First(x => x.OrderItemId == assembledItemId);
            Assert.False(assembledItem.IsCustom);
            Assert.Equal(300m, assembledItem.UnitPrice);
            Assert.Equal(900m, assembledItem.TotalPrice);

            Assert.Equal(1180m, result.SubTotal);
            Assert.Equal(1180m, result.TotalAmount);
        }

        [Fact]
        public async Task CheckoutAsync_PersistsCustomComponentPriceSnapshotsIntoOrderItems()
        {
            var userId = Guid.NewGuid();
            var shopId = Guid.NewGuid();
            var baseKitId = Guid.NewGuid();
            var partId = Guid.NewGuid();
            var cartItemId = Guid.NewGuid();

            var selectedParts = new Dictionary<string, SelectedPartResponse>
            {
                ["switch"] = new SelectedPartResponse
                {
                    Id = partId,
                    Name = "Switch X",
                    Price = 20m,
                    Quantity = 2,
                    ThumbnailUrl = "switch-x.png"
                }
            };

            var designConfig = JsonSerializer.Serialize(new
            {
                SessionId = Guid.NewGuid(),
                BaseKitId = baseKitId,
                SelectedItemsJson = JsonSerializer.Serialize(selectedParts)
            });

            var cart = new Cart
            {
                Id = Guid.NewGuid(),
                CustomerId = userId,
                CreatedAt = DateTime.UtcNow,
                CartItems = new List<CartItem>
                {
                    new()
                    {
                        Id = cartItemId,
                        ProductId = baseKitId,
                        Quantity = 2,
                        IsCustom = true,
                        DesignConfig = designConfig
                    }
                }
            };

            var baseKit = new Model
            {
                Id = baseKitId,
                Name = "Custom Base",
                Price = 100m,
                ShopId = shopId,
                StockQuantity = 50,
                IsActive = true
            };

            var cartsRepo = new Mock<ICartRepository>();
            cartsRepo.Setup(x => x.GetCartByUserIdAsync(userId)).ReturnsAsync(cart);

            var modelRepo = new Mock<IModelRepository>();
            modelRepo.Setup(x => x.GetByIdAsync(baseKitId, It.IsAny<CancellationToken>())).ReturnsAsync(baseKit);
            modelRepo.Setup(x => x.UpdateStockAsync(baseKitId, -2, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            modelRepo.Setup(x => x.UpdateStockAsync(partId, -4, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var shopRepo = new Mock<IShopRepository>();
            shopRepo.Setup(x => x.GetByIdAsync(shopId, It.IsAny<CancellationToken>())).ReturnsAsync(new ShopProfile { Id = shopId, ShopName = "Snapshot Shop" });

            var orderGroupRepo = new Mock<IOrderGroupRepository>();
            OrderGroup? savedGroup = null;
            orderGroupRepo.Setup(x => x.CreateAsync(It.IsAny<OrderGroup>(), It.IsAny<CancellationToken>()))
                .Callback<OrderGroup, CancellationToken>((g, _) => savedGroup = g)
                .Returns(Task.CompletedTask);

            var cartItemRepo = new Mock<ICartItemRepository>();
            cartItemRepo.Setup(x => x.RemoveRange(It.IsAny<IEnumerable<CartItem>>()));

            var paymentService = new Mock<IPaymentService>();
            paymentService.Setup(x => x.CreateCheckoutSessionAsync(It.IsAny<CreateCheckoutSessionRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CheckoutSessionResponse { PaymentUrl = "https://pay.test/session" });

            var uow = new Mock<IUnitOfWork>();
            uow.SetupGet(x => x.Carts).Returns(cartsRepo.Object);
            uow.SetupGet(x => x.Models).Returns(modelRepo.Object);
            uow.SetupGet(x => x.Shops).Returns(shopRepo.Object);
            uow.SetupGet(x => x.OrderGroups).Returns(orderGroupRepo.Object);
            uow.SetupGet(x => x.CartItems).Returns(cartItemRepo.Object);
            uow.SetupGet(x => x.Vouchers).Returns(new Mock<IVoucherRepository>().Object);
            uow.SetupGet(x => x.VoucherUsageLogs).Returns(new Mock<IVoucherUsageLogRepository>().Object);
            uow.SetupGet(x => x.AssembledProducts).Returns(new Mock<IAssembledProductRepository>().Object);
            uow.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            uow.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<CheckoutResponse>>>() ))
                .Returns((Func<Task<CheckoutResponse>> action) => action());

            var service = CreateOrderService(uow, paymentService);

            var checkout = await service.CheckoutAsync(userId, new CheckoutRequest
            {
                ReceiverName = "A",
                ReceiverPhone = "0123456789",
                ShippingAddress = "HCM",
                PaymentMethod = PaymentMethod.CreditCard,
                SelectedOrderItemIds = new List<Guid> { cartItemId },
                SuccessUrl = "https://ok",
                CancelUrl = "https://cancel"
            });

            Assert.NotNull(checkout);
            Assert.NotNull(savedGroup);
            Assert.Single(savedGroup!.Orders);

            var order = savedGroup.Orders.First();
            Assert.Single(order.OrderItems);

            var orderItem = order.OrderItems.First();
            Assert.Equal(140m, orderItem.UnitPrice);
            Assert.Equal(280m, orderItem.TotalPrice);
            Assert.Single(orderItem.OrderItemComponents);

            var component = orderItem.OrderItemComponents.First();
            Assert.Equal(partId, component.PartId);
            Assert.Equal("Switch X", component.PartName);
            Assert.Equal(20m, component.PartPriceSnapshot);
            Assert.Equal("switch-x.png", component.PartImageUrl);
            Assert.Equal(2, component.Quantity);

            modelRepo.Verify(x => x.UpdateStockAsync(baseKitId, -2, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
            modelRepo.Verify(x => x.UpdateStockAsync(partId, -4, It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        private static OrderService CreateOrderService(Mock<IUnitOfWork> unitOfWork, Mock<IPaymentService>? paymentServiceMock = null)
        {
            paymentServiceMock ??= new Mock<IPaymentService>();

            return new OrderService(
                unitOfWork.Object,
                new Mock<IMapper>().Object,
                paymentServiceMock.Object,
                new Mock<IVoucherService>().Object,
                new Mock<IWalletService>().Object,
                Options.Create(new OrderSettings
                {
                    DefaultShippingFee = 15000,
                    AbandonedOrderTimeoutHours = 24,
                    WarrantyPeriodDays = 7,
                    CancellationSpamCheckDays = 30,
                    MaxCancellationsPerPeriod = 3,
                    ShopCancellationPenaltyRate = 0,
                    ShopResponseTimeoutHours = 24
                }),
                Options.Create(new FrontendUrls
                {
                    PaymentSuccessPath = "https://ok",
                    PaymentCancelPath = "https://cancel"
                }),
                Options.Create(new SystemSettings()),
                Options.Create(new ReputationSettings()),
                new Mock<IReputationService>().Object,
                new Mock<IVnPayService>().Object,
                new Mock<IHttpContextAccessor>().Object);
        }
    }
}
