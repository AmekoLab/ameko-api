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
    /// <summary>
    /// Kiểm thử luồng mua hàng hoàn chỉnh với 2 kịch bản tranh chấp tồn kho (race condition):
    /// 
    /// Kịch bản 1 – Assembled Product:
    ///   Chỉ còn 1 sản phẩm Assembled trong kho, 2 người cùng checkout cùng lúc.
    ///   → Người đầu thành công, người thứ hai phải bị từ chối "hết hàng".
    /// 
    /// Kịch bản 2 – Custom Product:
    ///   Model (base kit) chỉ còn 1 trong kho, 2 người cùng custom và checkout cùng lúc.
    ///   → Người đầu thành công, người thứ hai phải bị từ chối "hết hàng".
    /// </summary>
    public class ConcurrentPurchaseFlowTests
    {
        // ===================================================================
        // IDs dùng chung
        // ===================================================================
        private readonly Guid _shopId = Guid.NewGuid();
        private readonly Guid _shopProfileId = Guid.NewGuid();

        // User A & B
        private readonly Guid _userAId = Guid.NewGuid();
        private readonly Guid _userBId = Guid.NewGuid();

        // Assembled Product (quantity = 1)
        private readonly Guid _assembledProductId = Guid.NewGuid();
        private readonly Guid _assembledBaseKitModelId = Guid.NewGuid();

        // Custom Product – BaseKit (stock = 1)
        private readonly Guid _customBaseKitId = Guid.NewGuid();
        private readonly Guid _switchPartId = Guid.NewGuid();

        // Builder sessions
        private readonly Guid _sessionAId = Guid.NewGuid();
        private readonly Guid _sessionBId = Guid.NewGuid();

        // ===================================================================
        // SCENARIO 1: Assembled Product – Chỉ còn 1, 2 người cùng mua
        // ===================================================================

        /// <summary>
        /// Luồng hoàn chỉnh: UserA thêm 1 assembled product vào giỏ → Checkout thành công.
        /// Assembled product có Quantity = 1, sau checkout sẽ giảm xuống 0.
        /// </summary>
        [Fact]
        public async Task AssembledProduct_FirstBuyer_CheckoutSucceeds()
        {
            // ── Arrange ──
            var cartItemAId = Guid.NewGuid();
            var (uow, paymentService, assembledProduct) = SetupAssembledProductMocks(
                userIdForCart: _userAId,
                cartItemId: cartItemAId,
                initialStock: 1);

            // Mock trừ kho thành công (lần đầu)
            int currentStock = 1;
            assembledProduct.Quantity = currentStock;

            var service = CreateOrderService(uow, paymentService);

            // ── Act ──
            var result = await service.CheckoutAsync(_userAId, new CheckoutRequest
            {
                ReceiverName = "User A",
                ReceiverPhone = "0901234567",
                ShippingAddress = "123 Đường ABC",
                PaymentMethod = PaymentMethod.CreditCard,
                SelectedOrderItemIds = new List<Guid> { cartItemAId },
                SuccessUrl = "https://ok",
                CancelUrl = "https://cancel"
            });

            // ── Assert ──
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.OrderGroupId);
            Assert.True(result.TotalAmount > 0);
        }

        /// <summary>
        /// Luồng hoàn chỉnh: UserB thêm cùng assembled product vào giỏ → Checkout thất bại
        /// vì UserA đã mua hết hàng (stock = 0 sau lần mua đầu).
        /// Mô phỏng: Sau khi UserA checkout, quantity = 0 → UserB detect hết hàng.
        /// </summary>
        [Fact]
        public async Task AssembledProduct_SecondBuyer_CheckoutFails_OutOfStock()
        {
            // ── Arrange ──
            var cartItemBId = Guid.NewGuid();
            var (uow, paymentService, assembledProduct) = SetupAssembledProductMocks(
                userIdForCart: _userBId,
                cartItemId: cartItemBId,
                initialStock: 0); // Stock = 0, đã bị UserA mua hết

            var service = CreateOrderService(uow, paymentService);

            // ── Act & Assert ──
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CheckoutAsync(_userBId, new CheckoutRequest
                {
                    ReceiverName = "User B",
                    ReceiverPhone = "0907654321",
                    ShippingAddress = "456 Đường DEF",
                    PaymentMethod = PaymentMethod.CreditCard,
                    SelectedOrderItemIds = new List<Guid> { cartItemBId },
                    SuccessUrl = "https://ok",
                    CancelUrl = "https://cancel"
                }));

            // Hệ thống phải phát hiện sản phẩm hết kho
            // ValidateCartItemStockAsync throws "Insufficient stock."
            // hoặc ProcessCheckoutShopGroupAsync throws "đã hết hàng"
            Assert.Contains("Insufficient stock", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Mô phỏng race condition thực tế: 2 user checkout cùng lúc (concurrent).
        /// Cả 2 đều đọc thấy stock = 1 nhưng chỉ 1 người có thể trừ kho thành công.
        /// </summary>
        [Fact]
        public async Task AssembledProduct_ConcurrentCheckout_OnlyOneSucceeds()
        {
            // ── Arrange ──
            var cartItemAId = Guid.NewGuid();
            var cartItemBId = Guid.NewGuid();
            int sharedStock = 1;
            var stockLock = new object();

            // Assembled Product dùng chung, Quantity = 1
            var assembledProduct = CreateTestAssembledProduct(sharedStock);

            // ── Setup shared mocks ──
            var assembledRepo = new Mock<IAssembledProductRepository>();

            // Cả 2 user đều đọc thấy product cùng 1 lúc
            assembledRepo.Setup(x => x.GetByIdWithDetailsAsync(_assembledProductId))
                .ReturnsAsync(() =>
                {
                    // Trả về product với quantity hiện tại (shared state)
                    var cloned = CloneAssembledProduct(assembledProduct);
                    cloned.Quantity = sharedStock;
                    return cloned;
                });

            // UpdateAsync sẽ race: chỉ 1 lần trừ kho thành công (atomic)
            assembledRepo.Setup(x => x.UpdateAsync(It.IsAny<AssembledProduct>()))
                .Returns((AssembledProduct ap) =>
                {
                    lock (stockLock)
                    {
                        if (sharedStock <= 0)
                            throw new InvalidOperationException($"Sản phẩm '{ap.Name}' đã hết hàng.");

                        sharedStock--;
                    }
                    return Task.CompletedTask;
                });

            // Setup cho UserA
            var (uowA, paymentA) = SetupUserMocksForConcurrent(_userAId, cartItemAId, assembledRepo);
            var serviceA = CreateOrderService(uowA, paymentA);

            // Setup cho UserB
            var (uowB, paymentB) = SetupUserMocksForConcurrent(_userBId, cartItemBId, assembledRepo);
            var serviceB = CreateOrderService(uowB, paymentB);

            var requestA = new CheckoutRequest
            {
                ReceiverName = "User A",
                ReceiverPhone = "0901234567",
                ShippingAddress = "123 Đường ABC",
                PaymentMethod = PaymentMethod.CreditCard,
                SelectedOrderItemIds = new List<Guid> { cartItemAId },
                SuccessUrl = "https://ok",
                CancelUrl = "https://cancel"
            };

            var requestB = new CheckoutRequest
            {
                ReceiverName = "User B",
                ReceiverPhone = "0907654321",
                ShippingAddress = "456 Đường DEF",
                PaymentMethod = PaymentMethod.CreditCard,
                SelectedOrderItemIds = new List<Guid> { cartItemBId },
                SuccessUrl = "https://ok",
                CancelUrl = "https://cancel"
            };

            // ── Act: Chạy 2 checkout song song ──
            var taskA = serviceA.CheckoutAsync(_userAId, requestA);
            var taskB = serviceB.CheckoutAsync(_userBId, requestB);

            var results = await Task.WhenAll(
                SafeCheckout(taskA),
                SafeCheckout(taskB)
            );

            // ── Assert: Chính xác 1 thành công, 1 thất bại ──
            int successCount = results.Count(r => r.Success);
            int failCount = results.Count(r => !r.Success);

            Assert.Equal(1, successCount);
            Assert.Equal(1, failCount);

            // Kiểm tra stock cuối cùng = 0
            Assert.Equal(0, sharedStock);
        }

        // ===================================================================
        // SCENARIO 2: Custom Product – BaseKit chỉ còn 1, 2 người cùng custom
        // ===================================================================

        /// <summary>
        /// Luồng hoàn chỉnh: UserA custom build → add to cart → checkout thành công.
        /// BaseKit có StockQuantity = 1, trừ kho thành công.
        /// </summary>
        [Fact]
        public async Task CustomProduct_FirstBuyer_CheckoutSucceeds()
        {
            // ── Arrange ──
            var cartItemAId = Guid.NewGuid();
            var (uow, paymentService) = SetupCustomProductMocks(
                userId: _userAId,
                cartItemId: cartItemAId,
                sessionId: _sessionAId,
                baseKitStock: 1,
                switchStock: 50,  // Switch có đủ
                stockUpdateSuccess: true);

            var service = CreateOrderService(uow, paymentService);

            // ── Act ──
            var result = await service.CheckoutAsync(_userAId, new CheckoutRequest
            {
                ReceiverName = "User A",
                ReceiverPhone = "0901234567",
                ShippingAddress = "123 Đường ABC",
                PaymentMethod = PaymentMethod.CreditCard,
                SelectedOrderItemIds = new List<Guid> { cartItemAId },
                SuccessUrl = "https://ok",
                CancelUrl = "https://cancel"
            });

            // ── Assert ──
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.OrderGroupId);
        }

        /// <summary>
        /// Luồng hoàn chỉnh: UserB custom build → add to cart → checkout THẤT BẠI
        /// vì BaseKit đã bị UserA lấy hết (stock = 0 sau lần mua đầu).
        /// </summary>
        [Fact]
        public async Task CustomProduct_SecondBuyer_CheckoutFails_BaseKitOutOfStock()
        {
            // ── Arrange ──
            var cartItemBId = Guid.NewGuid();
            var (uow, paymentService) = SetupCustomProductMocks(
                userId: _userBId,
                cartItemId: cartItemBId,
                sessionId: _sessionBId,
                baseKitStock: 0,  // Stock = 0, đã bị UserA mua hết
                switchStock: 50,
                stockUpdateSuccess: false);  // UpdateStock sẽ trả false (hết hàng)

            var service = CreateOrderService(uow, paymentService);

            // ── Act & Assert ──
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CheckoutAsync(_userBId, new CheckoutRequest
                {
                    ReceiverName = "User B",
                    ReceiverPhone = "0907654321",
                    ShippingAddress = "456 Đường DEF",
                    PaymentMethod = PaymentMethod.CreditCard,
                    SelectedOrderItemIds = new List<Guid> { cartItemBId },
                    SuccessUrl = "https://ok",
                    CancelUrl = "https://cancel"
                }));

            // Hệ thống phải chặn vì hết base kit
            // ValidateCartItemStockAsync throws "Insufficient stock for custom base kit."
            Assert.Contains("Insufficient stock", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Mô phỏng race condition thực tế: 2 user custom build cùng BaseKit (stock = 1),
        /// checkout cùng lúc. Chỉ 1 thành công.
        /// </summary>
        [Fact]
        public async Task CustomProduct_ConcurrentCheckout_OnlyOneSucceeds()
        {
            // ── Arrange ──
            var cartItemAId = Guid.NewGuid();
            var cartItemBId = Guid.NewGuid();
            int baseKitSharedStock = 1;
            var stockLock = new object();

            var modelRepo = new Mock<IModelRepository>();

            // BaseKit — cả 2 user đều đọc thấy model
            var baseKit = CreateTestBaseKit(baseKitSharedStock);
            modelRepo.Setup(x => x.GetByIdAsync(_customBaseKitId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    var cloned = CloneBaseKit(baseKit);
                    cloned.StockQuantity = baseKitSharedStock;
                    return cloned;
                });

            // Switch
            modelRepo.Setup(x => x.GetByIdAsync(_switchPartId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Model
                {
                    Id = _switchPartId,
                    Name = "Switch Blue",
                    Price = 15m,
                    StockQuantity = 100,
                    ShopId = _shopId,
                    IsActive = true
                });

            // UpdateStockAsync cho BaseKit – atomic: chỉ 1 lượt trừ thành công
            modelRepo.Setup(x => x.UpdateStockAsync(_customBaseKitId, It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, int delta, bool includeDeleted, CancellationToken ct) =>
                {
                    lock (stockLock)
                    {
                        if (baseKitSharedStock + delta < 0)
                            return false; // Hết hàng
                        baseKitSharedStock += delta;
                        return true;
                    }
                });

            // UpdateStockAsync cho Switch – luôn thành công (có nhiều)
            modelRepo.Setup(x => x.UpdateStockAsync(_switchPartId, It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Setup cho UserA
            var (uowA, paymentA) = SetupCustomUserMocksForConcurrent(_userAId, cartItemAId, _sessionAId, modelRepo);
            var serviceA = CreateOrderService(uowA, paymentA);

            // Setup cho UserB
            var (uowB, paymentB) = SetupCustomUserMocksForConcurrent(_userBId, cartItemBId, _sessionBId, modelRepo);
            var serviceB = CreateOrderService(uowB, paymentB);

            var requestA = new CheckoutRequest
            {
                ReceiverName = "User A",
                ReceiverPhone = "0901234567",
                ShippingAddress = "123 Đường ABC",
                PaymentMethod = PaymentMethod.CreditCard,
                SelectedOrderItemIds = new List<Guid> { cartItemAId },
                SuccessUrl = "https://ok",
                CancelUrl = "https://cancel"
            };

            var requestB = new CheckoutRequest
            {
                ReceiverName = "User B",
                ReceiverPhone = "0907654321",
                ShippingAddress = "456 Đường DEF",
                PaymentMethod = PaymentMethod.CreditCard,
                SelectedOrderItemIds = new List<Guid> { cartItemBId },
                SuccessUrl = "https://ok",
                CancelUrl = "https://cancel"
            };

            // ── Act: Chạy 2 checkout song song ──
            var taskA = serviceA.CheckoutAsync(_userAId, requestA);
            var taskB = serviceB.CheckoutAsync(_userBId, requestB);

            var results = await Task.WhenAll(
                SafeCheckout(taskA),
                SafeCheckout(taskB)
            );

            // ── Assert ──
            int successCount = results.Count(r => r.Success);
            int failCount = results.Count(r => !r.Success);

            Assert.Equal(1, successCount);
            Assert.Equal(1, failCount);
            Assert.Equal(0, baseKitSharedStock);
        }

        // ===================================================================
        // SCENARIO 3: Full Flow – AddToCart + Checkout hoàn chỉnh (cả 2 loại sản phẩm)
        // ===================================================================

        /// <summary>
        /// Test full flow: UserA add assembled product to cart → Checkout thành công.
        /// Kiểm tra validate stock khi add to cart.
        /// </summary>
        [Fact]
        public async Task FullFlow_AddAssembledToCart_ThenCheckout_Success()
        {
            // ── Arrange ──
            var cartId = Guid.NewGuid();
            var cartItemId = Guid.NewGuid();

            var assembledProduct = CreateTestAssembledProduct(1);

            var assembledRepo = new Mock<IAssembledProductRepository>();
            assembledRepo.Setup(x => x.GetByIdWithDetailsAsync(_assembledProductId))
                .ReturnsAsync(assembledProduct);
            assembledRepo.Setup(x => x.UpdateAsync(It.IsAny<AssembledProduct>()))
                .Returns(Task.CompletedTask);

            // Cart ban đầu rỗng
            var emptyCart = new Cart
            {
                Id = cartId,
                CustomerId = _userAId,
                CreatedAt = DateTime.UtcNow,
                CartItems = new List<CartItem>()
            };

            // Cart sau khi đã add item
            var cartWithItem = new Cart
            {
                Id = cartId,
                CustomerId = _userAId,
                CreatedAt = DateTime.UtcNow,
                CartItems = new List<CartItem>
                {
                    new()
                    {
                        Id = cartItemId,
                        CartId = cartId,
                        AssembledProductId = _assembledProductId,
                        Quantity = 1,
                        IsCustom = false
                    }
                }
            };

            var cartsRepo = new Mock<ICartRepository>();
            var addToCartCallCount = 0;
            cartsRepo.Setup(x => x.GetCartByUserIdAsync(_userAId))
                .ReturnsAsync(() =>
                {
                    addToCartCallCount++;
                    // Lần đầu trả cart rỗng (cho AddToCart), lần sau trả cart có item (cho Checkout)
                    return addToCartCallCount <= 1 ? emptyCart : cartWithItem;
                });

            var cartItemRepo = new Mock<ICartItemRepository>();
            cartItemRepo.Setup(x => x.AddAsync(It.IsAny<CartItem>())).Returns(Task.CompletedTask);
            cartItemRepo.Setup(x => x.RemoveRange(It.IsAny<IEnumerable<CartItem>>()));

            var modelRepo = new Mock<IModelRepository>();
            modelRepo.Setup(x => x.GetByIdAsync(_assembledBaseKitModelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Model
                {
                    Id = _assembledBaseKitModelId,
                    Name = "Base Model",
                    Price = 100m,
                    ShopId = _shopId
                });

            var shopRepo = CreateShopRepo();
            var orderGroupRepo = CreateOrderGroupRepo();

            var paymentService = new Mock<IPaymentService>();
            paymentService.Setup(x => x.CreateCheckoutSessionAsync(It.IsAny<CreateCheckoutSessionRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CheckoutSessionResponse { PaymentUrl = "https://pay.test/session" });

            var uow = new Mock<IUnitOfWork>();
            uow.SetupGet(x => x.Carts).Returns(cartsRepo.Object);
            uow.SetupGet(x => x.CartItems).Returns(cartItemRepo.Object);
            uow.SetupGet(x => x.Models).Returns(modelRepo.Object);
            uow.SetupGet(x => x.AssembledProducts).Returns(assembledRepo.Object);
            uow.SetupGet(x => x.Shops).Returns(shopRepo.Object);
            uow.SetupGet(x => x.OrderGroups).Returns(orderGroupRepo.Object);
            uow.SetupGet(x => x.Vouchers).Returns(new Mock<IVoucherRepository>().Object);
            uow.SetupGet(x => x.VoucherUsageLogs).Returns(new Mock<IVoucherUsageLogRepository>().Object);
            uow.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            uow.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<CheckoutResponse>>>()))
                .Returns((Func<Task<CheckoutResponse>> action) => action());

            var service = CreateOrderService(uow, paymentService);

            // ── Act 1: Add to Cart ──
            await service.AddToCartAsync(_userAId, new AddToCartRequest
            {
                ProductId = _assembledProductId,
                Quantity = 1,
                IsCustom = false
            });

            // ── Act 2: Checkout ──
            var result = await service.CheckoutAsync(_userAId, new CheckoutRequest
            {
                ReceiverName = "User A",
                ReceiverPhone = "0901234567",
                ShippingAddress = "123 Đường ABC",
                PaymentMethod = PaymentMethod.CreditCard,
                SelectedOrderItemIds = new List<Guid> { cartItemId },
                SuccessUrl = "https://ok",
                CancelUrl = "https://cancel"
            });

            // ── Assert ──
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.OrderGroupId);
        }

        /// <summary>
        /// UserB thử add assembled product nhưng stock = 0 → lỗi ngay từ bước AddToCart.
        /// </summary>
        [Fact]
        public async Task FullFlow_AddAssembledToCart_Fails_WhenOutOfStock()
        {
            // ── Arrange ──
            var assembledProduct = CreateTestAssembledProduct(0); // stock = 0

            var assembledRepo = new Mock<IAssembledProductRepository>();
            assembledRepo.Setup(x => x.GetByIdWithDetailsAsync(_assembledProductId))
                .ReturnsAsync(assembledProduct);

            var cart = new Cart
            {
                Id = Guid.NewGuid(),
                CustomerId = _userBId,
                CreatedAt = DateTime.UtcNow,
                CartItems = new List<CartItem>()
            };

            var cartsRepo = new Mock<ICartRepository>();
            cartsRepo.Setup(x => x.GetCartByUserIdAsync(_userBId)).ReturnsAsync(cart);

            var uow = new Mock<IUnitOfWork>();
            uow.SetupGet(x => x.Carts).Returns(cartsRepo.Object);
            uow.SetupGet(x => x.AssembledProducts).Returns(assembledRepo.Object);
            uow.SetupGet(x => x.CartItems).Returns(new Mock<ICartItemRepository>().Object);
            uow.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);

            var service = CreateOrderService(uow);

            // ── Act & Assert ──
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.AddToCartAsync(_userBId, new AddToCartRequest
                {
                    ProductId = _assembledProductId,
                    Quantity = 1,
                    IsCustom = false
                }));

            Assert.Contains("Insufficient stock", exception.Message);
        }

        /// <summary>
        /// UserA performs custom build add to cart (via builder session) → Checkout thành công.
        /// UserB cùng session khác nhưng cùng BaseKit (stock = 1) → add to cart OK nhưng checkout thất bại.
        /// </summary>
        [Fact]
        public async Task FullFlow_AddCustomToCart_SecondUser_CheckoutFails()
        {
            // ── Arrange ──
            var cartItemBId = Guid.NewGuid();
            var (uowB, paymentB) = SetupCustomProductMocks(
                userId: _userBId,
                cartItemId: cartItemBId,
                sessionId: _sessionBId,
                baseKitStock: 0,  // UserA đã mua hết
                switchStock: 50,
                stockUpdateSuccess: false); // UpdateStock trả false

            var serviceB = CreateOrderService(uowB, paymentB);

            // ── Act & Assert ──
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                serviceB.CheckoutAsync(_userBId, new CheckoutRequest
                {
                    ReceiverName = "User B",
                    ReceiverPhone = "0907654321",
                    ShippingAddress = "456 Đường DEF",
                    PaymentMethod = PaymentMethod.CreditCard,
                    SelectedOrderItemIds = new List<Guid> { cartItemBId },
                    SuccessUrl = "https://ok",
                    CancelUrl = "https://cancel"
                }));

            Assert.Contains("Insufficient stock", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ===================================================================
        // SCENARIO 4: Validate Stock trong Cart khi stock giảm sau khi add
        // ===================================================================

        /// <summary>
        /// UserB đã add 1 assembled product vào giỏ khi còn hàng.
        /// Nhưng trước khi checkout, UserA đã mua hết → stock giảm xuống 0.
        /// Khi UserB checkout → ValidateCartItemStockAsync phát hiện hết hàng, throw exception.
        /// </summary>
        [Fact]
        public async Task AssembledProduct_StockDepleted_BetweenAddAndCheckout_Fails()
        {
            // ── Arrange ──
            var cartItemBId = Guid.NewGuid();

            // UserB đã có item trong giỏ (add khi còn stock = 1)
            var assembledProduct = CreateTestAssembledProduct(0); // Nhưng bây giờ stock = 0

            var (uow, paymentService, _) = SetupAssembledProductMocks(
                userIdForCart: _userBId,
                cartItemId: cartItemBId,
                initialStock: 0);

            var service = CreateOrderService(uow, paymentService);

            // ── Act & Assert ──
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CheckoutAsync(_userBId, new CheckoutRequest
                {
                    ReceiverName = "User B",
                    ReceiverPhone = "0907654321",
                    ShippingAddress = "456 Đường DEF",
                    PaymentMethod = PaymentMethod.CreditCard,
                    SelectedOrderItemIds = new List<Guid> { cartItemBId },
                    SuccessUrl = "https://ok",
                    CancelUrl = "https://cancel"
                }));
        }

        /// <summary>
        /// UserB đã add custom product khi base kit còn hàng.
        /// Trước khi checkout, UserA mua hết → base kit stock = 0.
        /// ValidateCartItemStockAsync phải chặn UserB.
        /// </summary>
        [Fact]
        public async Task CustomProduct_StockDepleted_BetweenAddAndCheckout_Fails()
        {
            // ── Arrange ──
            var cartItemBId = Guid.NewGuid();

            // Base kit stock = 0 (UserA đã mua)
            var (uow, paymentService) = SetupCustomProductMocks(
                userId: _userBId,
                cartItemId: cartItemBId,
                sessionId: _sessionBId,
                baseKitStock: 0,
                switchStock: 50,
                stockUpdateSuccess: false);

            var service = CreateOrderService(uow, paymentService);

            // ── Act & Assert ──
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CheckoutAsync(_userBId, new CheckoutRequest
                {
                    ReceiverName = "User B",
                    ReceiverPhone = "0907654321",
                    ShippingAddress = "456 Đường DEF",
                    PaymentMethod = PaymentMethod.CreditCard,
                    SelectedOrderItemIds = new List<Guid> { cartItemBId },
                    SuccessUrl = "https://ok",
                    CancelUrl = "https://cancel"
                }));
        }

        // ===================================================================
        // HELPER: SafeCheckout – Bắt exception để WhenAll không bị crash
        // ===================================================================

        private record CheckoutResult(bool Success, CheckoutResponse? Response, string? Error);

        private static async Task<CheckoutResult> SafeCheckout(Task<CheckoutResponse> task)
        {
            try
            {
                var response = await task;
                return new CheckoutResult(true, response, null);
            }
            catch (Exception ex)
            {
                return new CheckoutResult(false, null, ex.Message);
            }
        }

        // ===================================================================
        // PRIVATE SETUP HELPERS
        // ===================================================================

        /// <summary>
        /// Tạo toàn bộ mock cho kịch bản Assembled Product.
        /// </summary>
        private (Mock<IUnitOfWork> uow, Mock<IPaymentService> payment, AssembledProduct assembledProduct) SetupAssembledProductMocks(
            Guid userIdForCart, Guid cartItemId, int initialStock)
        {
            var assembledProduct = CreateTestAssembledProduct(initialStock);

            var cart = new Cart
            {
                Id = Guid.NewGuid(),
                CustomerId = userIdForCart,
                CreatedAt = DateTime.UtcNow,
                CartItems = new List<CartItem>
                {
                    new()
                    {
                        Id = cartItemId,
                        AssembledProductId = _assembledProductId,
                        Quantity = 1,
                        IsCustom = false
                    }
                }
            };

            var cartsRepo = new Mock<ICartRepository>();
            cartsRepo.Setup(x => x.GetCartByUserIdAsync(userIdForCart)).ReturnsAsync(cart);

            var assembledRepo = new Mock<IAssembledProductRepository>();
            assembledRepo.Setup(x => x.GetByIdWithDetailsAsync(_assembledProductId))
                .ReturnsAsync(assembledProduct);
            assembledRepo.Setup(x => x.UpdateAsync(It.IsAny<AssembledProduct>()))
                .Returns(Task.CompletedTask);

            var modelRepo = new Mock<IModelRepository>();
            modelRepo.Setup(x => x.GetByIdAsync(_assembledBaseKitModelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Model
                {
                    Id = _assembledBaseKitModelId,
                    Name = "Keyboard Base Kit",
                    Price = 100m,
                    ShopId = _shopId
                });

            var shopRepo = CreateShopRepo();
            var orderGroupRepo = CreateOrderGroupRepo();

            var cartItemRepo = new Mock<ICartItemRepository>();
            cartItemRepo.Setup(x => x.RemoveRange(It.IsAny<IEnumerable<CartItem>>()));

            var paymentService = new Mock<IPaymentService>();
            paymentService.Setup(x => x.CreateCheckoutSessionAsync(It.IsAny<CreateCheckoutSessionRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CheckoutSessionResponse { PaymentUrl = "https://pay.test/session" });

            var uow = new Mock<IUnitOfWork>();
            uow.SetupGet(x => x.Carts).Returns(cartsRepo.Object);
            uow.SetupGet(x => x.CartItems).Returns(cartItemRepo.Object);
            uow.SetupGet(x => x.Models).Returns(modelRepo.Object);
            uow.SetupGet(x => x.AssembledProducts).Returns(assembledRepo.Object);
            uow.SetupGet(x => x.Shops).Returns(shopRepo.Object);
            uow.SetupGet(x => x.OrderGroups).Returns(orderGroupRepo.Object);
            uow.SetupGet(x => x.Vouchers).Returns(new Mock<IVoucherRepository>().Object);
            uow.SetupGet(x => x.VoucherUsageLogs).Returns(new Mock<IVoucherUsageLogRepository>().Object);
            uow.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            uow.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<CheckoutResponse>>>()))
                .Returns((Func<Task<CheckoutResponse>> action) => action());

            return (uow, paymentService, assembledProduct);
        }

        /// <summary>
        /// Tạo toàn bộ mock cho kịch bản Custom Product.
        /// </summary>
        private (Mock<IUnitOfWork> uow, Mock<IPaymentService> payment) SetupCustomProductMocks(
            Guid userId, Guid cartItemId, Guid sessionId,
            int baseKitStock, int switchStock, bool stockUpdateSuccess)
        {
            var selectedParts = new Dictionary<string, SelectedPartResponse>
            {
                ["switch"] = new SelectedPartResponse
                {
                    Id = _switchPartId,
                    Name = "Switch Blue",
                    Price = 15m,
                    Quantity = 2,
                    ThumbnailUrl = "switch.png"
                }
            };

            var designConfig = JsonSerializer.Serialize(new
            {
                SessionId = sessionId,
                BaseKitId = _customBaseKitId,
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
                        ProductId = _customBaseKitId,
                        Quantity = 1,
                        IsCustom = true,
                        DesignConfig = designConfig
                    }
                }
            };

            var cartsRepo = new Mock<ICartRepository>();
            cartsRepo.Setup(x => x.GetCartByUserIdAsync(userId)).ReturnsAsync(cart);

            var baseKit = CreateTestBaseKit(baseKitStock);

            var modelRepo = new Mock<IModelRepository>();
            modelRepo.Setup(x => x.GetByIdAsync(_customBaseKitId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(baseKit);
            modelRepo.Setup(x => x.GetByIdAsync(_switchPartId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Model
                {
                    Id = _switchPartId,
                    Name = "Switch Blue",
                    Price = 15m,
                    StockQuantity = switchStock,
                    ShopId = _shopId,
                    IsActive = true
                });
            modelRepo.Setup(x => x.UpdateStockAsync(_customBaseKitId, It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(stockUpdateSuccess);
            modelRepo.Setup(x => x.UpdateStockAsync(_switchPartId, It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true); // Switch luôn đủ

            var assembledRepo = new Mock<IAssembledProductRepository>();
            var shopRepo = CreateShopRepo();
            var orderGroupRepo = CreateOrderGroupRepo();

            var cartItemRepo = new Mock<ICartItemRepository>();
            cartItemRepo.Setup(x => x.RemoveRange(It.IsAny<IEnumerable<CartItem>>()));

            var paymentService = new Mock<IPaymentService>();
            paymentService.Setup(x => x.CreateCheckoutSessionAsync(It.IsAny<CreateCheckoutSessionRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CheckoutSessionResponse { PaymentUrl = "https://pay.test/session" });

            var uow = new Mock<IUnitOfWork>();
            uow.SetupGet(x => x.Carts).Returns(cartsRepo.Object);
            uow.SetupGet(x => x.CartItems).Returns(cartItemRepo.Object);
            uow.SetupGet(x => x.Models).Returns(modelRepo.Object);
            uow.SetupGet(x => x.AssembledProducts).Returns(assembledRepo.Object);
            uow.SetupGet(x => x.Shops).Returns(shopRepo.Object);
            uow.SetupGet(x => x.OrderGroups).Returns(orderGroupRepo.Object);
            uow.SetupGet(x => x.Vouchers).Returns(new Mock<IVoucherRepository>().Object);
            uow.SetupGet(x => x.VoucherUsageLogs).Returns(new Mock<IVoucherUsageLogRepository>().Object);
            uow.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            uow.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<CheckoutResponse>>>()))
                .Returns((Func<Task<CheckoutResponse>> action) => action());

            return (uow, paymentService);
        }

        /// <summary>
        /// Setup mocks cho 1 user trong kịch bản concurrent assembled product.
        /// Dùng chung assembledRepo (shared state).
        /// </summary>
        private (Mock<IUnitOfWork> uow, Mock<IPaymentService> payment) SetupUserMocksForConcurrent(
            Guid userId, Guid cartItemId, Mock<IAssembledProductRepository> sharedAssembledRepo)
        {
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
                        AssembledProductId = _assembledProductId,
                        Quantity = 1,
                        IsCustom = false
                    }
                }
            };

            var cartsRepo = new Mock<ICartRepository>();
            cartsRepo.Setup(x => x.GetCartByUserIdAsync(userId)).ReturnsAsync(cart);

            var modelRepo = new Mock<IModelRepository>();
            modelRepo.Setup(x => x.GetByIdAsync(_assembledBaseKitModelId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Model
                {
                    Id = _assembledBaseKitModelId,
                    Name = "Base Model",
                    Price = 100m,
                    ShopId = _shopId
                });

            var shopRepo = CreateShopRepo();
            var orderGroupRepo = CreateOrderGroupRepo();

            var cartItemRepo = new Mock<ICartItemRepository>();
            cartItemRepo.Setup(x => x.RemoveRange(It.IsAny<IEnumerable<CartItem>>()));

            var paymentService = new Mock<IPaymentService>();
            paymentService.Setup(x => x.CreateCheckoutSessionAsync(It.IsAny<CreateCheckoutSessionRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CheckoutSessionResponse { PaymentUrl = "https://pay.test/session" });

            var uow = new Mock<IUnitOfWork>();
            uow.SetupGet(x => x.Carts).Returns(cartsRepo.Object);
            uow.SetupGet(x => x.CartItems).Returns(cartItemRepo.Object);
            uow.SetupGet(x => x.Models).Returns(modelRepo.Object);
            uow.SetupGet(x => x.AssembledProducts).Returns(sharedAssembledRepo.Object);
            uow.SetupGet(x => x.Shops).Returns(shopRepo.Object);
            uow.SetupGet(x => x.OrderGroups).Returns(orderGroupRepo.Object);
            uow.SetupGet(x => x.Vouchers).Returns(new Mock<IVoucherRepository>().Object);
            uow.SetupGet(x => x.VoucherUsageLogs).Returns(new Mock<IVoucherUsageLogRepository>().Object);
            uow.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            uow.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<CheckoutResponse>>>()))
                .Returns((Func<Task<CheckoutResponse>> action) => action());

            return (uow, paymentService);
        }

        /// <summary>
        /// Setup mocks cho 1 user trong kịch bản concurrent custom product.
        /// Dùng chung modelRepo (shared state).
        /// </summary>
        private (Mock<IUnitOfWork> uow, Mock<IPaymentService> payment) SetupCustomUserMocksForConcurrent(
            Guid userId, Guid cartItemId, Guid sessionId, Mock<IModelRepository> sharedModelRepo)
        {
            var selectedParts = new Dictionary<string, SelectedPartResponse>
            {
                ["switch"] = new SelectedPartResponse
                {
                    Id = _switchPartId,
                    Name = "Switch Blue",
                    Price = 15m,
                    Quantity = 2,
                    ThumbnailUrl = "switch.png"
                }
            };

            var designConfig = JsonSerializer.Serialize(new
            {
                SessionId = sessionId,
                BaseKitId = _customBaseKitId,
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
                        ProductId = _customBaseKitId,
                        Quantity = 1,
                        IsCustom = true,
                        DesignConfig = designConfig
                    }
                }
            };

            var cartsRepo = new Mock<ICartRepository>();
            cartsRepo.Setup(x => x.GetCartByUserIdAsync(userId)).ReturnsAsync(cart);

            var assembledRepo = new Mock<IAssembledProductRepository>();
            var shopRepo = CreateShopRepo();
            var orderGroupRepo = CreateOrderGroupRepo();

            var cartItemRepo = new Mock<ICartItemRepository>();
            cartItemRepo.Setup(x => x.RemoveRange(It.IsAny<IEnumerable<CartItem>>()));

            var paymentService = new Mock<IPaymentService>();
            paymentService.Setup(x => x.CreateCheckoutSessionAsync(It.IsAny<CreateCheckoutSessionRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CheckoutSessionResponse { PaymentUrl = "https://pay.test/session" });

            var uow = new Mock<IUnitOfWork>();
            uow.SetupGet(x => x.Carts).Returns(cartsRepo.Object);
            uow.SetupGet(x => x.CartItems).Returns(cartItemRepo.Object);
            uow.SetupGet(x => x.Models).Returns(sharedModelRepo.Object);
            uow.SetupGet(x => x.AssembledProducts).Returns(assembledRepo.Object);
            uow.SetupGet(x => x.Shops).Returns(shopRepo.Object);
            uow.SetupGet(x => x.OrderGroups).Returns(orderGroupRepo.Object);
            uow.SetupGet(x => x.Vouchers).Returns(new Mock<IVoucherRepository>().Object);
            uow.SetupGet(x => x.VoucherUsageLogs).Returns(new Mock<IVoucherUsageLogRepository>().Object);
            uow.Setup(x => x.CommitAsync()).Returns(Task.CompletedTask);
            uow.Setup(x => x.ExecuteInTransactionAsync(It.IsAny<Func<Task<CheckoutResponse>>>()))
                .Returns((Func<Task<CheckoutResponse>> action) => action());

            return (uow, paymentService);
        }

        // ===================================================================
        // ENTITY FACTORIES
        // ===================================================================

        private AssembledProduct CreateTestAssembledProduct(int quantity) => new()
        {
            Id = _assembledProductId,
            Name = "AMK68 Wireless Keyboard",
            Price = 2500000m,
            Image1 = "amk68.jpg",
            Quantity = quantity,
            Layout = "65%",
            Mounting = "Gasket",
            Connection = "Bluetooth 5.0",
            ProductAssembledDetails = new List<ProductAssembledDetail>
            {
                new()
                {
                    AssembledProductId = _assembledProductId,
                    BaseKitId = _assembledBaseKitModelId,
                    ComponentId = Guid.Empty,
                    Quantity = 1
                }
            }
        };

        private Model CreateTestBaseKit(int stock) => new()
        {
            Id = _customBaseKitId,
            Name = "Custom Base Kit 75%",
            Price = 1500000m,
            ShopId = _shopId,
            StockQuantity = stock,
            IsActive = true,
            ThumbnailURL = "basekit75.jpg"
        };

        private AssembledProduct CloneAssembledProduct(AssembledProduct source) => new()
        {
            Id = source.Id,
            Name = source.Name,
            Price = source.Price,
            Image1 = source.Image1,
            Quantity = source.Quantity,
            Layout = source.Layout,
            Mounting = source.Mounting,
            Connection = source.Connection,
            ProductAssembledDetails = source.ProductAssembledDetails
        };

        private Model CloneBaseKit(Model source) => new()
        {
            Id = source.Id,
            Name = source.Name,
            Price = source.Price,
            ShopId = source.ShopId,
            StockQuantity = source.StockQuantity,
            IsActive = source.IsActive,
            ThumbnailURL = source.ThumbnailURL
        };

        private Mock<IShopRepository> CreateShopRepo()
        {
            var shopRepo = new Mock<IShopRepository>();
            shopRepo.Setup(x => x.GetByIdAsync(_shopId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ShopProfile
                {
                    Id = _shopProfileId,
                    ShopName = "AMK Collective Store",
                    UserId = Guid.NewGuid()
                });
            return shopRepo;
        }

        private Mock<IOrderGroupRepository> CreateOrderGroupRepo()
        {
            var orderGroupRepo = new Mock<IOrderGroupRepository>();
            orderGroupRepo.Setup(x => x.CreateAsync(It.IsAny<OrderGroup>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            return orderGroupRepo;
        }

        // ===================================================================
        // SERVICE FACTORY
        // ===================================================================

        private static OrderService CreateOrderService(
            Mock<IUnitOfWork> unitOfWork,
            Mock<IPaymentService>? paymentServiceMock = null)
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
                    DefaultShippingFee = 30000,
                    AbandonedOrderTimeoutHours = 24,
                    WarrantyPeriodDays = 30,
                    CancellationSpamCheckDays = 7,
                    MaxCancellationsPerPeriod = 4,
                    ShopCancellationPenaltyRate = 0.05m,
                    ShopResponseTimeoutHours = 24
                }),
                Options.Create(new FrontendUrls
                {
                    PaymentSuccessPath = "https://amk.com/success",
                    PaymentCancelPath = "https://amk.com/cancel"
                }),
                Options.Create(new SystemSettings()),
                Options.Create(new ReputationSettings()),
                new Mock<IReputationService>().Object,
                new Mock<IVnPayService>().Object,
                new Mock<IHttpContextAccessor>().Object,
                new Mock<FPTU.Capstone.AMKCollective.Application.Interfaces.AI.IAIService>().Object);
        }
    }
}
