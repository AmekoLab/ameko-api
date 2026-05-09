using FPTU.Capstone.AMKCollective.Application.DTOs.ShopDashboard;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Moq;
using Xunit;

namespace FPTU.Capstone.AMKCollective.Tests
{
    public class ShopDashboardServiceTests
    {
        [Fact]
        public async Task GetCustomerBehaviorOverviewAsync_ComputesCoreBehaviorMetrics()
        {
            var shopUserId = Guid.NewGuid();
            var shopId = Guid.NewGuid();
            var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);

            var customerA = CreateCustomer("Alice", "Tran", "alice", "a@test.com");
            var customerB = CreateCustomer("Bob", "Nguyen", "bob", "b@test.com");

            var orders = new List<Order>
            {
                new() { ShopId = shopId, CustomerId = customerA.Id, Customer = customerA, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 100, CreatedAt = from.AddDays(1) },
                new() { ShopId = shopId, CustomerId = customerA.Id, Customer = customerA, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 120, CreatedAt = from.AddDays(8) },
                new() { ShopId = shopId, CustomerId = customerB.Id, Customer = customerB, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 90, CreatedAt = from.AddDays(5) }
            };

            var service = CreateService(shopUserId, shopId, orders);

            var result = await service.GetCustomerBehaviorOverviewAsync(shopUserId, new ShopBehaviorFilterRequest
            {
                StartDate = from,
                EndDate = to
            });

            Assert.Equal(2, result.TotalCustomers);
            Assert.Equal(2, result.NewCustomers);
            Assert.Equal(0, result.ReturningCustomers);
            Assert.Equal(1, result.RepeatCustomers);
            Assert.Equal(50m, result.RepeatRate);
            Assert.Equal(103.33m, result.AverageOrderValue);
            Assert.Equal(1.50m, result.PurchaseFrequency);
            Assert.Equal(3, result.TotalOrders);
            Assert.Equal(310m, result.TotalRevenue);
        }

        [Fact]
        public async Task GetTopSpendersAsync_ReturnsSortedPaginatedData()
        {
            var shopUserId = Guid.NewGuid();
            var shopId = Guid.NewGuid();
            var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);

            var customerA = CreateCustomer("Alice", "Tran", "alice", "a@test.com");
            var customerB = CreateCustomer("Bob", "Nguyen", "bob", "b@test.com");

            var orders = new List<Order>
            {
                new() { ShopId = shopId, CustomerId = customerA.Id, Customer = customerA, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 80, CreatedAt = from.AddDays(3) },
                new() { ShopId = shopId, CustomerId = customerB.Id, Customer = customerB, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 200, CreatedAt = from.AddDays(4) },
                new() { ShopId = shopId, CustomerId = customerA.Id, Customer = customerA, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 70, CreatedAt = from.AddDays(10) }
            };

            var service = CreateService(shopUserId, shopId, orders);

            var result = await service.GetTopSpendersAsync(shopUserId, new ShopBehaviorFilterRequest
            {
                StartDate = from,
                EndDate = to,
                PageNumber = 1,
                PageSize = 1
            });

            Assert.Equal(2, result.TotalCount);
            Assert.Single(result.Items);
            Assert.Equal(customerB.Id, result.Items.First().CustomerId);
            Assert.Equal(200m, result.Items.First().TotalSpent);
        }

        [Fact]
        public async Task GetChurnRiskCustomersAsync_FiltersByInactivityDays()
        {
            var shopUserId = Guid.NewGuid();
            var shopId = Guid.NewGuid();
            var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);

            var stale = CreateCustomer("Stale", "User", "stale", "s@test.com");
            var active = CreateCustomer("Active", "User", "active", "a@test.com");

            var orders = new List<Order>
            {
                new() { ShopId = shopId, CustomerId = stale.Id, Customer = stale, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 100, CreatedAt = from.AddDays(1) },
                new() { ShopId = shopId, CustomerId = active.Id, Customer = active, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 150, CreatedAt = to.AddDays(-2) }
            };

            var service = CreateService(shopUserId, shopId, orders);

            var result = await service.GetChurnRiskCustomersAsync(shopUserId, new ShopBehaviorFilterRequest
            {
                StartDate = from,
                EndDate = to,
                ChurnDays = 10,
                PageNumber = 1,
                PageSize = 10
            });

            Assert.Single(result.Items);
            Assert.Equal(stale.Id, result.Items.First().CustomerId);
        }

        [Fact]
        public async Task GetCustomerBehaviorOverviewAsync_ThrowsWhenShopNotFound()
        {
            var uow = new Mock<IUnitOfWork>();
            var shopRepo = new Mock<IShopRepository>();
            shopRepo.Setup(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ShopProfile?)null);
            uow.SetupGet(x => x.Shops).Returns(shopRepo.Object);

            var service = new ShopDashboardService(uow.Object);

            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetCustomerBehaviorOverviewAsync(Guid.NewGuid(), new ShopBehaviorFilterRequest()));
        }

        [Fact]
        public async Task GetCustomerBehaviorOverviewAsync_ExcludesDeletedAndInCartOrders_AndDetectsReturningCustomers()
        {
            var shopUserId = Guid.NewGuid();
            var shopId = Guid.NewGuid();
            var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);

            var returningCustomer = CreateCustomer("Returning", "User", "returning", "r@test.com");
            var newCustomer = CreateCustomer("New", "User", "new", "n@test.com");

            var orders = new List<Order>
            {
                new() { ShopId = shopId, CustomerId = returningCustomer.Id, Customer = returningCustomer, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 50, CreatedAt = from.AddDays(-10) },
                new() { ShopId = shopId, CustomerId = returningCustomer.Id, Customer = returningCustomer, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 80, CreatedAt = from.AddDays(1) },
                new() { ShopId = shopId, CustomerId = newCustomer.Id, Customer = newCustomer, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 100, CreatedAt = from.AddDays(2) },
                new() { ShopId = shopId, CustomerId = newCustomer.Id, Customer = newCustomer, OrderStatus = OrderStatus.InCart, PaymentStatus = PaymentStatus.Pending, TotalAmount = 999, CreatedAt = from.AddDays(3) },
                new() { ShopId = shopId, CustomerId = newCustomer.Id, Customer = newCustomer, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 777, CreatedAt = from.AddDays(4), IsDeleted = true }
            };

            var service = CreateService(shopUserId, shopId, orders);

            var result = await service.GetCustomerBehaviorOverviewAsync(shopUserId, new ShopBehaviorFilterRequest
            {
                StartDate = from,
                EndDate = to
            });

            Assert.Equal(2, result.TotalCustomers);
            Assert.Equal(1, result.NewCustomers);
            Assert.Equal(1, result.ReturningCustomers);
            Assert.Equal(0, result.RepeatCustomers);
            Assert.Equal(2, result.TotalOrders);
            Assert.Equal(180m, result.TotalRevenue);
        }

        [Fact]
        public async Task GetCustomerBehaviorTrendAsync_ReturnsZeroBucketWhenNoOrdersInRange()
        {
            var shopUserId = Guid.NewGuid();
            var shopId = Guid.NewGuid();
            var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);

            var customer = CreateCustomer("Trend", "User", "trend", "trend@test.com");
            var orders = new List<Order>
            {
                new() { ShopId = shopId, CustomerId = customer.Id, Customer = customer, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 50, CreatedAt = from.AddMonths(-1) }
            };

            var service = CreateService(shopUserId, shopId, orders);

            var result = await service.GetCustomerBehaviorTrendAsync(shopUserId, new ShopBehaviorFilterRequest
            {
                StartDate = from,
                EndDate = to,
                Granularity = "day"
            });

            Assert.Single(result);
            Assert.Equal(from.Date, result[0].BucketStartUtc);
            Assert.Equal(0, result[0].Orders);
            Assert.Equal(0m, result[0].Revenue);
            Assert.Equal(0, result[0].NewCustomers);
            Assert.Equal(0, result[0].ReturningCustomers);
        }

        [Fact]
        public async Task GetTopSpendersAsync_NormalizesInvalidPagingValues()
        {
            var shopUserId = Guid.NewGuid();
            var shopId = Guid.NewGuid();
            var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);

            var customers = Enumerable.Range(1, 3)
                .Select(i => CreateCustomer($"User{i}", "Test", $"u{i}", $"u{i}@test.com"))
                .ToList();

            var orders = new List<Order>
            {
                new() { ShopId = shopId, CustomerId = customers[0].Id, Customer = customers[0], OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 50, CreatedAt = from.AddDays(1) },
                new() { ShopId = shopId, CustomerId = customers[1].Id, Customer = customers[1], OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 70, CreatedAt = from.AddDays(2) },
                new() { ShopId = shopId, CustomerId = customers[2].Id, Customer = customers[2], OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 90, CreatedAt = from.AddDays(3) }
            };

            var service = CreateService(shopUserId, shopId, orders);

            var result = await service.GetTopSpendersAsync(shopUserId, new ShopBehaviorFilterRequest
            {
                StartDate = from,
                EndDate = to,
                PageNumber = 0,
                PageSize = 1000
            });

            Assert.Equal(1, result.CurrentPage);
            Assert.Equal(100, result.PageSize);
            Assert.Equal(3, result.TotalCount);
            Assert.Equal(3, result.Items.Count());
            Assert.Equal(customers[2].Id, result.Items.First().CustomerId);
        }

        [Fact]
        public async Task GetChurnRiskCustomersAsync_UsesDefaultThirtyDaysWhenChurnDaysInvalid()
        {
            var shopUserId = Guid.NewGuid();
            var shopId = Guid.NewGuid();
            var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);

            var oldCustomer = CreateCustomer("Old", "Customer", "old", "old@test.com");
            var recentCustomer = CreateCustomer("Recent", "Customer", "recent", "recent@test.com");

            var orders = new List<Order>
            {
                new() { ShopId = shopId, CustomerId = oldCustomer.Id, Customer = oldCustomer, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 200, CreatedAt = to.AddDays(-40) },
                new() { ShopId = shopId, CustomerId = recentCustomer.Id, Customer = recentCustomer, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 120, CreatedAt = to.AddDays(-10) }
            };

            var service = CreateService(shopUserId, shopId, orders);

            var result = await service.GetChurnRiskCustomersAsync(shopUserId, new ShopBehaviorFilterRequest
            {
                StartDate = from,
                EndDate = to,
                ChurnDays = 0,
                PageNumber = 1,
                PageSize = 10
            });

            Assert.Single(result.Items);
            Assert.Equal(oldCustomer.Id, result.Items.First().CustomerId);
            Assert.True(result.Items.First().InactiveDays >= 30);
        }

        [Fact]
        public async Task GetPurchaseFrequencyAsync_ComputesAverageDaysBetweenOrders()
        {
            var shopUserId = Guid.NewGuid();
            var shopId = Guid.NewGuid();
            var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);

            var customer = CreateCustomer("Freq", "Customer", "freq", "freq@test.com");
            var orders = new List<Order>
            {
                new() { ShopId = shopId, CustomerId = customer.Id, Customer = customer, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 10, CreatedAt = from.AddDays(1) },
                new() { ShopId = shopId, CustomerId = customer.Id, Customer = customer, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 20, CreatedAt = from.AddDays(4) },
                new() { ShopId = shopId, CustomerId = customer.Id, Customer = customer, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 30, CreatedAt = from.AddDays(9) }
            };

            var service = CreateService(shopUserId, shopId, orders);

            var result = await service.GetPurchaseFrequencyAsync(shopUserId, new ShopBehaviorFilterRequest
            {
                StartDate = from,
                EndDate = to
            });

            Assert.Equal(1, result.CustomersWithOrders);
            Assert.Equal(3, result.TotalOrders);
            Assert.Equal(3m, result.OrdersPerCustomer);
            Assert.Equal(4m, result.AverageDaysBetweenOrders);
        }

        [Fact]
        public async Task GetConversionSummaryAsync_ReturnsZeroRatesWhenNoOrdersInRange()
        {
            var shopUserId = Guid.NewGuid();
            var shopId = Guid.NewGuid();
            var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);

            var customer = CreateCustomer("Zero", "Range", "zero", "zero@test.com");
            var orders = new List<Order>
            {
                new() { ShopId = shopId, CustomerId = customer.Id, Customer = customer, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 100, CreatedAt = from.AddMonths(-1) }
            };

            var service = CreateService(shopUserId, shopId, orders);

            var result = await service.GetConversionSummaryAsync(shopUserId, new ShopBehaviorFilterRequest
            {
                StartDate = from,
                EndDate = to
            });

            Assert.Equal(0, result.TotalOrders);
            Assert.Equal(0, result.PaidOrders);
            Assert.Equal(0, result.CompletedOrders);
            Assert.Equal(0, result.CancelledOrders);
            Assert.Equal(0m, result.PaidRate);
            Assert.Equal(0m, result.CompletionRate);
            Assert.Equal(0m, result.CancelRate);
        }

        [Fact]
        public async Task GetCustomerBehaviorOverviewAsync_ThrowsWhenDateRangeInvalid()
        {
            var shopUserId = Guid.NewGuid();
            var shopId = Guid.NewGuid();
            var service = CreateService(shopUserId, shopId, new List<Order>());

            await Assert.ThrowsAsync<ArgumentException>(() => service.GetCustomerBehaviorOverviewAsync(shopUserId, new ShopBehaviorFilterRequest
            {
                StartDate = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)
            }));
        }

        [Fact]
        public async Task GetConversionSummaryAsync_IncludesOrdersAtUtcConvertedBoundaries()
        {
            var shopUserId = Guid.NewGuid();
            var shopId = Guid.NewGuid();
            var localFrom = DateTime.SpecifyKind(new DateTime(2026, 3, 15, 0, 0, 0), DateTimeKind.Local);
            var localTo = DateTime.SpecifyKind(new DateTime(2026, 3, 15, 23, 59, 59), DateTimeKind.Local);
            var fromUtc = localFrom.ToUniversalTime();
            var toUtc = localTo.ToUniversalTime();

            var customer = CreateCustomer("Boundary", "User", "boundary", "boundary@test.com");
            var orders = new List<Order>
            {
                new() { ShopId = shopId, CustomerId = customer.Id, Customer = customer, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 100, CreatedAt = fromUtc },
                new() { ShopId = shopId, CustomerId = customer.Id, Customer = customer, OrderStatus = OrderStatus.Cancelled, PaymentStatus = PaymentStatus.Pending, TotalAmount = 120, CreatedAt = toUtc },
                new() { ShopId = shopId, CustomerId = customer.Id, Customer = customer, OrderStatus = OrderStatus.Completed, PaymentStatus = PaymentStatus.Paid, TotalAmount = 50, CreatedAt = fromUtc.AddSeconds(-1) },
                new() { ShopId = shopId, CustomerId = customer.Id, Customer = customer, OrderStatus = OrderStatus.Refunded, PaymentStatus = PaymentStatus.Refunded, TotalAmount = 70, CreatedAt = toUtc.AddSeconds(1) }
            };

            var service = CreateService(shopUserId, shopId, orders);

            var result = await service.GetConversionSummaryAsync(shopUserId, new ShopBehaviorFilterRequest
            {
                StartDate = localFrom,
                EndDate = localTo
            });

            Assert.Equal(2, result.TotalOrders);
            Assert.Equal(1, result.PaidOrders);
            Assert.Equal(1, result.CompletedOrders);
            Assert.Equal(1, result.CancelledOrders);
            Assert.Equal(50m, result.PaidRate);
            Assert.Equal(50m, result.CompletionRate);
            Assert.Equal(50m, result.CancelRate);
        }

        [Fact]
        public async Task GetTopSpendersAsync_ProcessesLargeDatasetAndKeepsSorting()
        {
            var shopUserId = Guid.NewGuid();
            var shopId = Guid.NewGuid();
            var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);

            var customers = Enumerable.Range(1, 300)
                .Select(i => CreateCustomer($"Customer{i}", "Load", $"c{i}", $"c{i}@test.com"))
                .ToList();

            var orders = new List<Order>();
            for (var i = 0; i < customers.Count; i++)
            {
                for (var j = 0; j < 5; j++)
                {
                    orders.Add(new Order
                    {
                        ShopId = shopId,
                        CustomerId = customers[i].Id,
                        Customer = customers[i],
                        OrderStatus = OrderStatus.Completed,
                        PaymentStatus = PaymentStatus.Paid,
                        TotalAmount = i + 1,
                        CreatedAt = from.AddDays(j)
                    });
                }
            }

            var service = CreateService(shopUserId, shopId, orders);

            var result = await service.GetTopSpendersAsync(shopUserId, new ShopBehaviorFilterRequest
            {
                StartDate = from,
                EndDate = to,
                PageNumber = 1,
                PageSize = 100
            });

            Assert.Equal(300, result.TotalCount);
            Assert.Equal(1, result.CurrentPage);
            Assert.Equal(100, result.PageSize);
            Assert.Equal(100, result.Items.Count());
            Assert.Equal(customers.Last().Id, result.Items.First().CustomerId);
            Assert.Equal(1500m, result.Items.First().TotalSpent);
        }

        private static ShopDashboardService CreateService(Guid shopUserId, Guid shopId, List<Order> orders)
        {
            var uow = new Mock<IUnitOfWork>();

            var shopRepo = new Mock<IShopRepository>();
            shopRepo.Setup(x => x.GetByUserIdAsync(shopUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ShopProfile { Id = shopId, UserId = shopUserId, ShopName = "Test Shop" });

            var orderRepo = new Mock<IOrderRepository>();
            orderRepo.Setup(x => x.GetShopOrdersForDashboardAsync(shopId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid sId, DateTime? f, DateTime? t, bool inc, CancellationToken ct) => 
                {
                    if (inc) return orders;
                    return orders.Where(o => o.OrderStatus != OrderStatus.Cancelled && o.OrderStatus != OrderStatus.Refunded).ToList();
                });
            orderRepo.Setup(x => x.GetCustomerFirstOrderDatesAsync(shopId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<Guid, DateTime>());

            uow.SetupGet(x => x.Shops).Returns(shopRepo.Object);
            uow.SetupGet(x => x.Orders).Returns(orderRepo.Object);

            return new ShopDashboardService(uow.Object);
        }

        private static User CreateCustomer(string firstName, string lastName, string username, string email)
        {
            return new User
            {
                Id = Guid.NewGuid(),
                FirstName = firstName,
                LastName = lastName,
                Username = username,
                Email = email,
                HashedPassword = "hash",
                RoleId = Guid.NewGuid()
            };
        }
    }
}
