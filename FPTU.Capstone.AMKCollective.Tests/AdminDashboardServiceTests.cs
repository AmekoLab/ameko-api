using FPTU.Capstone.AMKCollective.Application.DTOs.AdminDashboard;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Moq;
using Xunit;

namespace FPTU.Capstone.AMKCollective.Tests
{
    public class AdminDashboardServiceTests
    {
        [Fact]
        public async Task GetOverviewAsync_ComputesCoreMetrics()
        {
            var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);

            var orders = new List<Order>
            {
                new() { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), ShopId = Guid.NewGuid(), TotalAmount = 100, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, CreatedAt = from.AddDays(1) },
                new() { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), ShopId = Guid.NewGuid(), TotalAmount = 200, OrderStatus = OrderStatus.Cancelled, PaymentStatus = PaymentStatus.Paid, CreatedAt = from.AddDays(2) },
                new() { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), ShopId = Guid.NewGuid(), TotalAmount = 50, OrderStatus = OrderStatus.Refunded, PaymentStatus = PaymentStatus.Refunded, CreatedAt = from.AddDays(3) },
                new() { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), ShopId = Guid.NewGuid(), TotalAmount = 999, OrderStatus = OrderStatus.InCart, PaymentStatus = PaymentStatus.Pending, CreatedAt = from.AddDays(4) }
            };

            var users = new List<User>
            {
                new() { Id = Guid.NewGuid(), FirstName = "A", LastName = "A", Username = "a", Email = "a@test.com", HashedPassword = "h", RoleId = Guid.NewGuid() },
                new() { Id = Guid.NewGuid(), FirstName = "B", LastName = "B", Username = "b", Email = "b@test.com", HashedPassword = "h", RoleId = Guid.NewGuid() }
            };

            var uow = BuildUnitOfWork(orders, users, new List<Payment>(), new List<OrderIssue>(), activeShopsCount: 4);
            var service = new AdminDashboardService(uow.Object);

            var result = await service.GetOverviewAsync(new AdminDashboardFilterRequest { StartDate = from, EndDate = to });

            Assert.Equal(3, result.TotalOrders);
            Assert.Equal(1, result.CompletedOrders);
            Assert.Equal(1, result.CancelledOrders);
            Assert.Equal(1, result.RefundedOrders);
            Assert.Equal(350, result.GrossMerchandiseValue);
            Assert.Equal(100, result.NetRevenue);
            Assert.Equal(3, result.ActiveBuyers);
            Assert.Equal(4, result.ActiveShops);
            Assert.Equal(2, result.NewUsers);
            Assert.Equal(33.33m, result.OrderCompletionRate);
        }

        [Fact]
        public async Task GetPaymentsHealthAsync_ComputesRatesAndBreakdowns()
        {
            var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);

            var payments = new List<Payment>
            {
                new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Amount = 100, Status = PaymentStatus.Paid, Method = PaymentMethod.CreditCard, Type = PaymentType.OrderPayment, CreatedAt = from.AddDays(1) },
                new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Amount = 120, Status = PaymentStatus.Released, Method = PaymentMethod.Wallet, Type = PaymentType.PaymentByWallet, CreatedAt = from.AddDays(2) },
                new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Amount = 20, Status = PaymentStatus.Failed, Method = PaymentMethod.CreditCard, Type = PaymentType.OrderPayment, CreatedAt = from.AddDays(3) },
                new() { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Amount = 10, Status = PaymentStatus.Refunded, Method = PaymentMethod.CreditCard, Type = PaymentType.Refund, CreatedAt = from.AddDays(4) }
            };

            var uow = BuildUnitOfWork(new List<Order>(), new List<User>(), payments, new List<OrderIssue>(), activeShopsCount: 0);
            var service = new AdminDashboardService(uow.Object);

            var result = await service.GetPaymentsHealthAsync(new AdminDashboardFilterRequest { StartDate = from, EndDate = to });

            Assert.Equal(4, result.TotalPayments);
            Assert.Equal(2, result.SuccessfulPayments);
            Assert.Equal(1, result.FailedPayments);
            Assert.Equal(1, result.RefundedPayments);
            Assert.Equal(220, result.SuccessfulPaymentVolume);
            Assert.Equal(50m, result.PaymentSuccessRate);
            Assert.NotEmpty(result.MethodMetrics);
            Assert.NotEmpty(result.TypeMetrics);
        }

        [Fact]
        public async Task GetRiskOverviewAsync_ComputesRiskRatios()
        {
            var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);

            var orders = new List<Order>
            {
                new() { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), TotalAmount = 100, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, CreatedAt = from.AddDays(1) },
                new() { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), TotalAmount = 200, OrderStatus = OrderStatus.Cancelled, PaymentStatus = PaymentStatus.Pending, CreatedAt = from.AddDays(2) },
                new() { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), TotalAmount = 50, OrderStatus = OrderStatus.Refunded, PaymentStatus = PaymentStatus.Refunded, CreatedAt = from.AddDays(3) }
            };

            var issues = new List<OrderIssue>
            {
                new() { Id = Guid.NewGuid(), OrderId = Guid.NewGuid(), UserId = Guid.NewGuid(), Type = OrderIssueType.CancelRequest, Status = OrderIssueStatus.Pending, CreatedAt = from.AddDays(2) },
                new() { Id = Guid.NewGuid(), OrderId = Guid.NewGuid(), UserId = Guid.NewGuid(), Type = OrderIssueType.ReturnRequest, Status = OrderIssueStatus.InProgress, CreatedAt = from.AddDays(3) },
                new() { Id = Guid.NewGuid(), OrderId = Guid.NewGuid(), UserId = Guid.NewGuid(), Type = OrderIssueType.WarrantyClaim, Status = OrderIssueStatus.Completed, CreatedAt = from.AddDays(4) }
            };

            var uow = BuildUnitOfWork(orders, new List<User>(), new List<Payment>(), issues, activeShopsCount: 0);
            var service = new AdminDashboardService(uow.Object);

            var result = await service.GetRiskOverviewAsync(new AdminDashboardFilterRequest { StartDate = from, EndDate = to });

            Assert.Equal(3, result.TotalOrders);
            Assert.Equal(3, result.TotalIssues);
            Assert.Equal(2, result.OpenIssues);
            Assert.Equal(1, result.CancelRequests);
            Assert.Equal(1, result.RefundRequests);
            Assert.Equal(2, result.DisputeRequests);
            Assert.Equal(1, result.CancelledOrders);
            Assert.Equal(1, result.RefundedOrders);
            Assert.Equal(33.33m, result.CancelRate);
            Assert.Equal(33.33m, result.RefundRate);
            Assert.Equal(100m, result.IssueRate);
        }

        [Fact]
        public async Task GetOverviewAsync_ThrowsWhenDateRangeInvalid()
        {
            var uow = BuildUnitOfWork(new List<Order>(), new List<User>(), new List<Payment>(), new List<OrderIssue>(), activeShopsCount: 0);
            var service = new AdminDashboardService(uow.Object);

            await Assert.ThrowsAsync<ArgumentException>(() => service.GetOverviewAsync(new AdminDashboardFilterRequest
            {
                StartDate = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)
            }));
        }

        [Fact]
        public async Task GetOverviewAsync_ReturnsZeroMetricsWhenOnlyInCartOrdersExist()
        {
            var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);

            var orders = new List<Order>
            {
                new() { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), ShopId = Guid.NewGuid(), TotalAmount = 999, OrderStatus = OrderStatus.InCart, PaymentStatus = PaymentStatus.Pending, CreatedAt = from.AddDays(1) },
                new() { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), ShopId = Guid.NewGuid(), TotalAmount = 888, OrderStatus = OrderStatus.InCart, PaymentStatus = PaymentStatus.Paid, CreatedAt = from.AddDays(2) }
            };

            var uow = BuildUnitOfWork(orders, new List<User>(), new List<Payment>(), new List<OrderIssue>(), activeShopsCount: 2);
            var service = new AdminDashboardService(uow.Object);

            var result = await service.GetOverviewAsync(new AdminDashboardFilterRequest { StartDate = from, EndDate = to });

            Assert.Equal(0, result.TotalOrders);
            Assert.Equal(0, result.CompletedOrders);
            Assert.Equal(0, result.CancelledOrders);
            Assert.Equal(0, result.RefundedOrders);
            Assert.Equal(0m, result.GrossMerchandiseValue);
            Assert.Equal(0m, result.NetRevenue);
            Assert.Equal(0, result.ActiveBuyers);
            Assert.Equal(0m, result.OrderCompletionRate);
            Assert.Equal(2, result.ActiveShops);
        }

        [Fact]
        public async Task GetOverviewAsync_CountsRefundedOrdersByPaymentStatus()
        {
            var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);

            var orders = new List<Order>
            {
                new() { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), ShopId = Guid.NewGuid(), TotalAmount = 100, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, CreatedAt = from.AddDays(1) },
                new() { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), ShopId = Guid.NewGuid(), TotalAmount = 80, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Refunded, CreatedAt = from.AddDays(2) }
            };

            var uow = BuildUnitOfWork(orders, new List<User>(), new List<Payment>(), new List<OrderIssue>(), activeShopsCount: 1);
            var service = new AdminDashboardService(uow.Object);

            var result = await service.GetOverviewAsync(new AdminDashboardFilterRequest { StartDate = from, EndDate = to });

            Assert.Equal(2, result.TotalOrders);
            Assert.Equal(1, result.RefundedOrders);
        }

        [Fact]
        public async Task GetPaymentsHealthAsync_ReturnsZeroRatesAndEmptyMetricsWhenNoPayments()
        {
            var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);

            var uow = BuildUnitOfWork(new List<Order>(), new List<User>(), new List<Payment>(), new List<OrderIssue>(), activeShopsCount: 0);
            var service = new AdminDashboardService(uow.Object);

            var result = await service.GetPaymentsHealthAsync(new AdminDashboardFilterRequest { StartDate = from, EndDate = to });

            Assert.Equal(0, result.TotalPayments);
            Assert.Equal(0, result.SuccessfulPayments);
            Assert.Equal(0, result.FailedPayments);
            Assert.Equal(0, result.RefundedPayments);
            Assert.Equal(0m, result.SuccessfulPaymentVolume);
            Assert.Equal(0m, result.PaymentSuccessRate);
            Assert.Empty(result.MethodMetrics);
            Assert.Empty(result.TypeMetrics);
        }

        [Fact]
        public async Task GetRiskOverviewAsync_CountsReturnedOrdersAsRefundedAndExcludesInCart()
        {
            var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);

            var orders = new List<Order>
            {
                new() { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), TotalAmount = 100, OrderStatus = OrderStatus.Returned, PaymentStatus = PaymentStatus.Refunded, CreatedAt = from.AddDays(1) },
                new() { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), TotalAmount = 200, OrderStatus = OrderStatus.Cancelled, PaymentStatus = PaymentStatus.Pending, CreatedAt = from.AddDays(2) },
                new() { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid(), TotalAmount = 300, OrderStatus = OrderStatus.InCart, PaymentStatus = PaymentStatus.Pending, CreatedAt = from.AddDays(3) }
            };

            var uow = BuildUnitOfWork(orders, new List<User>(), new List<Payment>(), new List<OrderIssue>(), activeShopsCount: 0);
            var service = new AdminDashboardService(uow.Object);

            var result = await service.GetRiskOverviewAsync(new AdminDashboardFilterRequest { StartDate = from, EndDate = to });

            Assert.Equal(2, result.TotalOrders);
            Assert.Equal(1, result.CancelledOrders);
            Assert.Equal(1, result.RefundedOrders);
            Assert.Equal(50m, result.CancelRate);
            Assert.Equal(50m, result.RefundRate);
        }

        [Fact]
        public async Task GetOverviewAsync_ConvertsLocalDateRangeToUtcBeforeQuerying()
        {
            var localFrom = DateTime.SpecifyKind(new DateTime(2026, 3, 15, 0, 0, 0), DateTimeKind.Local);
            var localTo = DateTime.SpecifyKind(new DateTime(2026, 3, 16, 23, 59, 59), DateTimeKind.Local);
            var expectedFrom = localFrom.ToUniversalTime();
            var expectedTo = localTo.ToUniversalTime();

            var orderRepo = new Mock<IOrderRepository>();
            orderRepo.Setup(x => x.GetOrdersForDashboardAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Order>());

            var userRepo = new Mock<IUserRepository>();
            userRepo.Setup(x => x.GetUsersForDashboardAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User>());

            var paymentRepo = new Mock<IPaymentRepository>();
            paymentRepo.Setup(x => x.GetPaymentsForDashboardAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Payment>());

            var issueRepo = new Mock<IOrderIssueRepository>();
            issueRepo.Setup(x => x.GetIssuesForDashboardAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<OrderIssue>());

            var shopRepo = new Mock<IShopRepository>();
            shopRepo.Setup(x => x.GetShopsAsync(It.IsAny<string?>(), It.IsAny<ShopStatus?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((new List<ShopProfile>(), 0));

            var uow = new Mock<IUnitOfWork>();
            uow.SetupGet(x => x.Orders).Returns(orderRepo.Object);
            uow.SetupGet(x => x.Users).Returns(userRepo.Object);
            uow.SetupGet(x => x.Payments).Returns(paymentRepo.Object);
            uow.SetupGet(x => x.OrderIssues).Returns(issueRepo.Object);
            uow.SetupGet(x => x.Shops).Returns(shopRepo.Object);

            var service = new AdminDashboardService(uow.Object);

            await service.GetOverviewAsync(new AdminDashboardFilterRequest
            {
                StartDate = localFrom,
                EndDate = localTo
            });

            orderRepo.Verify(x => x.GetOrdersForDashboardAsync(expectedFrom, expectedTo, It.IsAny<CancellationToken>()), Times.Once);
            userRepo.Verify(x => x.GetUsersForDashboardAsync(expectedFrom, expectedTo, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetOverviewAsync_ComputesCorrectMetricsForLargeDataset()
        {
            var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);
            var customers = Enumerable.Range(0, 250).Select(_ => Guid.NewGuid()).ToList();

            var orders = Enumerable.Range(1, 2500)
                .Select(i => new Order
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customers[i % customers.Count],
                    ShopId = Guid.NewGuid(),
                    TotalAmount = 10 + (i % 7),
                    CreatedAt = from.AddMinutes(i),
                    OrderStatus = i % 10 == 0
                        ? OrderStatus.InCart
                        : i % 4 == 0
                            ? OrderStatus.Cancelled
                            : i % 6 == 0
                                ? OrderStatus.Refunded
                                : OrderStatus.Completed,
                    PaymentStatus = i % 6 == 0
                        ? PaymentStatus.Refunded
                        : i % 3 == 0
                            ? PaymentStatus.Paid
                            : PaymentStatus.Pending
                })
                .ToList();

            var users = Enumerable.Range(1, 80)
                .Select(_ => new User
                {
                    Id = Guid.NewGuid(),
                    FirstName = "Load",
                    LastName = "User",
                    Username = Guid.NewGuid().ToString("N"),
                    Email = $"{Guid.NewGuid():N}@test.com",
                    HashedPassword = "hash",
                    RoleId = Guid.NewGuid()
                })
                .ToList();

            var businessOrders = orders.Where(o => o.OrderStatus != OrderStatus.InCart).ToList();
            var paidOrders = businessOrders.Where(o => o.PaymentStatus == PaymentStatus.Paid).ToList();
            var expectedTotal = businessOrders.Count;
            var expectedCompleted = businessOrders.Count(o => o.OrderStatus == OrderStatus.Completed);
            var expectedCancelled = businessOrders.Count(o => o.OrderStatus == OrderStatus.Cancelled);
            var expectedRefunded = businessOrders.Count(o => o.OrderStatus == OrderStatus.Refunded || o.PaymentStatus == PaymentStatus.Refunded);
            var expectedGmv = businessOrders.Sum(o => o.TotalAmount);
            var expectedNet = paidOrders.Where(o => o.OrderStatus != OrderStatus.Cancelled && o.OrderStatus != OrderStatus.Refunded).Sum(o => o.TotalAmount);
            var expectedActiveBuyers = businessOrders.Select(o => o.CustomerId).Distinct().Count();
            var expectedCompletionRate = expectedTotal == 0 ? 0m : Math.Round((decimal)expectedCompleted * 100m / expectedTotal, 2);

            var uow = BuildUnitOfWork(orders, users, new List<Payment>(), new List<OrderIssue>(), activeShopsCount: 15);
            var service = new AdminDashboardService(uow.Object);

            var result = await service.GetOverviewAsync(new AdminDashboardFilterRequest { StartDate = from, EndDate = to });

            Assert.Equal(expectedTotal, result.TotalOrders);
            Assert.Equal(expectedCompleted, result.CompletedOrders);
            Assert.Equal(expectedCancelled, result.CancelledOrders);
            Assert.Equal(expectedRefunded, result.RefundedOrders);
            Assert.Equal(expectedGmv, result.GrossMerchandiseValue);
            Assert.Equal(expectedNet, result.NetRevenue);
            Assert.Equal(expectedActiveBuyers, result.ActiveBuyers);
            Assert.Equal(expectedCompletionRate, result.OrderCompletionRate);
            Assert.Equal(15, result.ActiveShops);
            Assert.Equal(80, result.NewUsers);
        }

        private static Mock<IUnitOfWork> BuildUnitOfWork(
            List<Order> orders,
            List<User> users,
            List<Payment> payments,
            List<OrderIssue> issues,
            int activeShopsCount)
        {
            var orderRepo = new Mock<IOrderRepository>();
            orderRepo.Setup(x => x.GetOrdersForDashboardAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(orders);

            var userRepo = new Mock<IUserRepository>();
            userRepo.Setup(x => x.GetUsersForDashboardAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(users);

            var paymentRepo = new Mock<IPaymentRepository>();
            paymentRepo.Setup(x => x.GetPaymentsForDashboardAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(payments);

            var issueRepo = new Mock<IOrderIssueRepository>();
            issueRepo.Setup(x => x.GetIssuesForDashboardAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(issues);

            var shopRepo = new Mock<IShopRepository>();
            shopRepo.Setup(x => x.GetShopsAsync(It.IsAny<string?>(), It.IsAny<ShopStatus?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((new List<ShopProfile>(), activeShopsCount));

            var uow = new Mock<IUnitOfWork>();
            uow.SetupGet(x => x.Orders).Returns(orderRepo.Object);
            uow.SetupGet(x => x.Users).Returns(userRepo.Object);
            uow.SetupGet(x => x.Payments).Returns(paymentRepo.Object);
            uow.SetupGet(x => x.OrderIssues).Returns(issueRepo.Object);
            uow.SetupGet(x => x.Shops).Returns(shopRepo.Object);

            return uow;
        }
    }
}
