using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Order;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Application.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FPTU.Capstone.AMKCollective.Tests
{
    public class ReproductionTests
    {
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _shopId = Guid.NewGuid();
        private readonly Guid _assembledProductId = Guid.NewGuid();
        private readonly Guid _baseKitId = Guid.NewGuid();

        private Mock<IUnitOfWork> CreateUowMock(
            AssembledProduct assembledProduct, 
            Model baseKit, 
            Cart cart)
        {
            var uow = new Mock<IUnitOfWork>();
            
            var assembledRepo = new Mock<IAssembledProductRepository>();
            assembledRepo.Setup(x => x.GetByIdWithDetailsAsync(_assembledProductId))
                .ReturnsAsync(assembledProduct);
            
            var modelRepo = new Mock<IModelRepository>();
            modelRepo.Setup(x => x.GetByIdAsync(_baseKitId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(baseKit);
            modelRepo.Setup(x => x.GetShopIdByAssembledProductAsync(_assembledProductId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(_shopId);

            var cartRepo = new Mock<ICartRepository>();
            cartRepo.Setup(x => x.GetCartByUserIdAsync(_userId))
                .ReturnsAsync(cart);

            var cartItemRepo = new Mock<ICartItemRepository>();
            
            var shopRepo = new Mock<IShopRepository>();
            shopRepo.Setup(x => x.GetByIdAsync(_shopId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ShopProfile { Id = _shopId, ShopName = "Test Shop", UserId = Guid.NewGuid() });

            uow.SetupGet(x => x.AssembledProducts).Returns(assembledRepo.Object);
            uow.SetupGet(x => x.Models).Returns(modelRepo.Object);
            uow.SetupGet(x => x.Carts).Returns(cartRepo.Object);
            uow.SetupGet(x => x.CartItems).Returns(cartItemRepo.Object);
            uow.SetupGet(x => x.Shops).Returns(shopRepo.Object);
            
            return uow;
        }

        private OrderService CreateOrderService(IUnitOfWork uow)
        {
            var mapper = new Mock<IMapper>();
            // Minimal mapper setup for OrderItemResponse
            mapper.Setup(m => m.Map<OrderItemResponse>(It.IsAny<CartItem>()))
                .Returns((CartItem src) => new OrderItemResponse { OrderItemId = src.Id, Quantity = src.Quantity });

            var paymentService = new Mock<IPaymentService>();
            var voucherService = new Mock<IVoucherService>();
            var walletService = new Mock<IWalletService>();
            var vnPayService = new Mock<IVnPayService>();
            var reputationService = new Mock<IReputationService>();
            var httpContextAccessor = new Mock<IHttpContextAccessor>();
            var aiService = new Mock<FPTU.Capstone.AMKCollective.Application.Interfaces.AI.IAIService>();

            var orderSettings = Options.Create(new OrderSettings { DefaultShippingFee = 30000 });
            var urlSettings = Options.Create(new FrontendUrls());
            var systemSettings = Options.Create(new SystemSettings());
            var reputationSettings = Options.Create(new ReputationSettings());

            return new OrderService(
                uow, 
                mapper.Object, 
                paymentService.Object, 
                voucherService.Object, 
                walletService.Object, 
                orderSettings,
                urlSettings, 
                systemSettings, 
                reputationSettings, 
                reputationService.Object, 
                vnPayService.Object, 
                httpContextAccessor.Object,
                aiService.Object);
        }

        [Fact]
        public async Task AddToCart_ShouldFail_IfComponentOutOfStock()
        {
            // Arrange
            var baseKit = new Model { Id = _baseKitId, Name = "Base Kit", StockQuantity = 0, IsActive = true, IsDeleted = false };
            var assembledProduct = new AssembledProduct 
            { 
                Id = _assembledProductId, 
                Name = "Assembled Keyboard", 
                Quantity = 10,
                ProductAssembledDetails = new List<ProductAssembledDetail> 
                { 
                    new ProductAssembledDetail { BaseKitId = _baseKitId, Quantity = 1, BaseKit = baseKit }
                }
            };
            var cart = new Cart { CustomerId = _userId, CartItems = new List<CartItem>() };
            
            var uow = CreateUowMock(assembledProduct, baseKit, cart);
            var service = CreateOrderService(uow.Object);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => 
                service.AddToCartAsync(_userId, new AddToCartRequest { ProductId = _assembledProductId, Quantity = 1, IsCustom = false }));
            
            Assert.Contains("không đủ tồn kho", exception.Message);
        }

        [Fact]
        public async Task AddToCart_ShouldFail_IfComponentDeleted()
        {
            // Arrange
            var baseKit = new Model { Id = _baseKitId, Name = "Base Kit", StockQuantity = 100, IsActive = true, IsDeleted = true };
            var assembledProduct = new AssembledProduct 
            { 
                Id = _assembledProductId, 
                Name = "Assembled Keyboard", 
                Quantity = 10,
                ProductAssembledDetails = new List<ProductAssembledDetail> 
                { 
                    new ProductAssembledDetail { BaseKitId = _baseKitId, Quantity = 1, BaseKit = baseKit }
                }
            };
            var cart = new Cart { CustomerId = _userId, CartItems = new List<CartItem>() };
            
            var uow = CreateUowMock(assembledProduct, baseKit, cart);
            var service = CreateOrderService(uow.Object);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => 
                service.AddToCartAsync(_userId, new AddToCartRequest { ProductId = _assembledProductId, Quantity = 1, IsCustom = false }));
            
            Assert.Contains("hiện không khả dụng", exception.Message);
        }

        [Fact]
        public async Task GetMyCart_ShouldShowUnavailable_IfComponentDeleted()
        {
            // Arrange
            var cartItemId = Guid.NewGuid();
            var baseKit = new Model { Id = _baseKitId, Name = "Base Kit", StockQuantity = 100, IsActive = true, IsDeleted = true };
            var assembledProduct = new AssembledProduct 
            { 
                Id = _assembledProductId, 
                Name = "Assembled Keyboard", 
                Quantity = 10,
                ProductAssembledDetails = new List<ProductAssembledDetail> 
                { 
                    new ProductAssembledDetail { BaseKitId = _baseKitId, Quantity = 1, BaseKit = baseKit }
                }
            };
            var cart = new Cart 
            { 
                CustomerId = _userId, 
                CartItems = new List<CartItem> 
                { 
                    new CartItem { Id = cartItemId, AssembledProductId = _assembledProductId, Quantity = 1, IsCustom = false }
                } 
            };
            
            var uow = CreateUowMock(assembledProduct, baseKit, cart);
            var service = CreateOrderService(uow.Object);

            // Act
            var result = await service.GetMyCartAsync(_userId);

            // Assert
            var cartItem = result.OrderItems.FirstOrDefault(ci => ci.OrderItemId == cartItemId);
            Assert.NotNull(cartItem);
            Assert.False(cartItem.IsAvailable);
            Assert.Contains("không còn khả dụng", cartItem.StatusMessage);
        }
    }
}
