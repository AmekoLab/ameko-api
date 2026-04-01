using FPTU.Capstone.AMKCollective.Application.DTOs.Common;
using FPTU.Capstone.AMKCollective.Application.DTOs.ShopDashboard;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class ShopDashboardService : IShopDashboardService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ShopDashboardService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ShopCustomerBehaviorOverviewResponse> GetCustomerBehaviorOverviewAsync(Guid shopUserId, ShopBehaviorFilterRequest filter)
        {
            var (shop, fromUtc, toUtc, allOrders, rangeOrders) = await LoadShopOrdersAsync(shopUserId, filter);
            var _ = shop;

            var allFirstOrderDateByCustomer = allOrders
                .GroupBy(o => o.CustomerId)
                .ToDictionary(g => g.Key, g => g.Min(x => x.CreatedAt));

            var customerOrderCountsInRange = rangeOrders
                .GroupBy(o => o.CustomerId)
                .ToDictionary(g => g.Key, g => g.Count());

            var totalCustomers = customerOrderCountsInRange.Count;
            var newCustomers = customerOrderCountsInRange.Keys.Count(customerId => allFirstOrderDateByCustomer[customerId] >= fromUtc && allFirstOrderDateByCustomer[customerId] <= toUtc);
            var returningCustomers = totalCustomers - newCustomers;
            var repeatCustomers = customerOrderCountsInRange.Count(x => x.Value >= 2);
            var totalOrders = rangeOrders.Count;
            var totalRevenue = rangeOrders.Sum(o => o.TotalAmount);

            return new ShopCustomerBehaviorOverviewResponse
            {
                FromUtc = fromUtc,
                ToUtc = toUtc,
                TotalCustomers = totalCustomers,
                NewCustomers = newCustomers,
                ReturningCustomers = returningCustomers,
                RepeatCustomers = repeatCustomers,
                RepeatRate = Rate(repeatCustomers, totalCustomers),
                AverageOrderValue = SafeAverage(totalRevenue, totalOrders),
                PurchaseFrequency = SafeAverage(totalOrders, totalCustomers),
                TotalOrders = totalOrders,
                TotalRevenue = totalRevenue
            };
        }

        public async Task<List<ShopCustomerTrendResponse>> GetCustomerBehaviorTrendAsync(Guid shopUserId, ShopBehaviorFilterRequest filter)
        {
            var (_, fromUtc, toUtc, allOrders, rangeOrders) = await LoadShopOrdersAsync(shopUserId, filter);

            var firstOrderByCustomer = allOrders
                .GroupBy(o => o.CustomerId)
                .ToDictionary(g => g.Key, g => g.Min(x => x.CreatedAt));

            var normalizedGranularity = (filter.Granularity ?? "day").Trim().ToLowerInvariant();
            var buckets = rangeOrders
                .GroupBy(o => BucketStart(o.CreatedAt, normalizedGranularity))
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var customerIds = g.Select(x => x.CustomerId).Distinct().ToList();
                    var newCustomers = customerIds.Count(customerId => IsSameBucket(firstOrderByCustomer[customerId], g.Key, normalizedGranularity));
                    var returningCustomers = customerIds.Count - newCustomers;

                    return new ShopCustomerTrendResponse
                    {
                        BucketStartUtc = g.Key,
                        NewCustomers = newCustomers,
                        ReturningCustomers = returningCustomers,
                        Orders = g.Count(),
                        Revenue = g.Sum(x => x.TotalAmount)
                    };
                })
                .ToList();

            if (buckets.Count == 0)
            {
                return new List<ShopCustomerTrendResponse>
                {
                    new()
                    {
                        BucketStartUtc = BucketStart(fromUtc, normalizedGranularity),
                        NewCustomers = 0,
                        ReturningCustomers = 0,
                        Orders = 0,
                        Revenue = 0
                    }
                };
            }

            return buckets;
        }

        public async Task<PaginatedResult<ShopTopSpenderResponse>> GetTopSpendersAsync(Guid shopUserId, ShopBehaviorFilterRequest filter)
        {
            var (_, fromUtc, toUtc, _, rangeOrders) = await LoadShopOrdersAsync(shopUserId, filter);
            var page = NormalizePage(filter.PageNumber);
            var size = NormalizePageSize(filter.PageSize);

            var grouped = rangeOrders
                .GroupBy(o => o.CustomerId)
                .Select(g =>
                {
                    var latestOrder = g.OrderByDescending(x => x.CreatedAt).First();
                    return new ShopTopSpenderResponse
                    {
                        CustomerId = g.Key,
                        CustomerName = BuildCustomerName(latestOrder.Customer),
                        Email = latestOrder.Customer?.Email ?? string.Empty,
                        Orders = g.Count(),
                        TotalSpent = g.Sum(x => x.TotalAmount),
                        LastOrderAtUtc = latestOrder.CreatedAt
                    };
                })
                .OrderByDescending(x => x.TotalSpent)
                .ThenByDescending(x => x.Orders)
                .ToList();

            var items = grouped.Skip((page - 1) * size).Take(size).ToList();
            return new PaginatedResult<ShopTopSpenderResponse>(items, grouped.Count, page, size);
        }

        public async Task<PaginatedResult<ShopChurnRiskCustomerResponse>> GetChurnRiskCustomersAsync(Guid shopUserId, ShopBehaviorFilterRequest filter)
        {
            var (_, _, toUtc, allOrders, _) = await LoadShopOrdersAsync(shopUserId, filter);
            var page = NormalizePage(filter.PageNumber);
            var size = NormalizePageSize(filter.PageSize);
            var churnDays = filter.ChurnDays <= 0 ? 30 : filter.ChurnDays;
            var thresholdDate = toUtc.Date.AddDays(-churnDays);

            var customers = allOrders
                .GroupBy(o => o.CustomerId)
                .Select(g =>
                {
                    var lastOrder = g.OrderByDescending(x => x.CreatedAt).First();
                    var inactiveDays = Math.Max(0, (toUtc.Date - lastOrder.CreatedAt.Date).Days);

                    return new ShopChurnRiskCustomerResponse
                    {
                        CustomerId = g.Key,
                        CustomerName = BuildCustomerName(lastOrder.Customer),
                        Email = lastOrder.Customer?.Email ?? string.Empty,
                        LifetimeOrders = g.Count(),
                        LifetimeValue = g.Sum(x => x.TotalAmount),
                        LastOrderAtUtc = lastOrder.CreatedAt,
                        InactiveDays = inactiveDays
                    };
                })
                .Where(x => x.LastOrderAtUtc < thresholdDate)
                .OrderByDescending(x => x.InactiveDays)
                .ThenByDescending(x => x.LifetimeValue)
                .ToList();

            var items = customers.Skip((page - 1) * size).Take(size).ToList();
            return new PaginatedResult<ShopChurnRiskCustomerResponse>(items, customers.Count, page, size);
        }

        public async Task<ShopPurchaseFrequencyResponse> GetPurchaseFrequencyAsync(Guid shopUserId, ShopBehaviorFilterRequest filter)
        {
            var (_, fromUtc, toUtc, _, rangeOrders) = await LoadShopOrdersAsync(shopUserId, filter);
            var groups = rangeOrders.GroupBy(o => o.CustomerId).ToList();

            decimal avgDaysBetweenOrders = 0;
            var gaps = new List<double>();

            foreach (var group in groups)
            {
                var dates = group.Select(x => x.CreatedAt).OrderBy(x => x).ToList();
                for (var i = 1; i < dates.Count; i++)
                {
                    gaps.Add((dates[i] - dates[i - 1]).TotalDays);
                }
            }

            if (gaps.Count > 0)
            {
                avgDaysBetweenOrders = Math.Round((decimal)gaps.Average(), 2);
            }

            return new ShopPurchaseFrequencyResponse
            {
                FromUtc = fromUtc,
                ToUtc = toUtc,
                CustomersWithOrders = groups.Count,
                TotalOrders = rangeOrders.Count,
                OrdersPerCustomer = SafeAverage(rangeOrders.Count, groups.Count),
                AverageDaysBetweenOrders = avgDaysBetweenOrders
            };
        }

        public async Task<ShopConversionResponse> GetConversionSummaryAsync(Guid shopUserId, ShopBehaviorFilterRequest filter)
        {
            var (_, fromUtc, toUtc, _, rangeOrders) = await LoadShopOrdersAsync(shopUserId, filter);

            var totalOrders = rangeOrders.Count;
            var paidOrders = rangeOrders.Count(o => o.PaymentStatus == PaymentStatus.Paid || o.PaymentStatus == PaymentStatus.Released);
            var completedOrders = rangeOrders.Count(o => o.OrderStatus == OrderStatus.Completed);
            var cancelledOrders = rangeOrders.Count(o => o.OrderStatus == OrderStatus.Cancelled || o.OrderStatus == OrderStatus.Refunded);

            return new ShopConversionResponse
            {
                FromUtc = fromUtc,
                ToUtc = toUtc,
                TotalOrders = totalOrders,
                PaidOrders = paidOrders,
                CompletedOrders = completedOrders,
                CancelledOrders = cancelledOrders,
                PaidRate = Rate(paidOrders, totalOrders),
                CompletionRate = Rate(completedOrders, totalOrders),
                CancelRate = Rate(cancelledOrders, totalOrders)
            };
        }

        private async Task<(ShopProfile shop, DateTime fromUtc, DateTime toUtc, List<Order> allOrders, List<Order> rangeOrders)> LoadShopOrdersAsync(Guid shopUserId, ShopBehaviorFilterRequest filter)
        {
            var shop = await _unitOfWork.Shops.GetByUserIdAsync(shopUserId);
            if (shop == null)
            {
                throw new KeyNotFoundException("Shop profile not found.");
            }

            var (fromUtc, toUtc) = ResolveDateRange(filter);
            var orders = (await _unitOfWork.Orders.GetOrdersByShopIdAsync(shop.Id))
                .Where(o => !o.IsDeleted && o.OrderStatus != OrderStatus.InCart)
                .ToList();

            var rangeOrders = orders
                .Where(o => o.CreatedAt >= fromUtc && o.CreatedAt <= toUtc)
                .ToList();

            return (shop, fromUtc, toUtc, orders, rangeOrders);
        }

        private static (DateTime fromUtc, DateTime toUtc) ResolveDateRange(ShopBehaviorFilterRequest filter)
        {
            var nowUtc = DateTime.UtcNow;
            var fromUtc = filter.StartDate?.ToUniversalTime() ?? new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var toUtc = filter.EndDate?.ToUniversalTime() ?? nowUtc;

            if (fromUtc > toUtc)
            {
                throw new ArgumentException("StartDate must be earlier than or equal to EndDate.");
            }

            return (fromUtc, toUtc);
        }

        private static decimal Rate(int numerator, int denominator)
        {
            if (denominator <= 0)
            {
                return 0m;
            }

            return Math.Round((decimal)numerator * 100m / denominator, 2);
        }

        private static decimal SafeAverage(decimal numerator, int denominator)
        {
            if (denominator <= 0)
            {
                return 0m;
            }

            return Math.Round(numerator / denominator, 2);
        }

        private static int NormalizePage(int pageNumber) => pageNumber <= 0 ? 1 : pageNumber;

        private static int NormalizePageSize(int pageSize)
        {
            if (pageSize <= 0)
            {
                return 10;
            }

            return pageSize > 100 ? 100 : pageSize;
        }

        private static DateTime BucketStart(DateTime date, string granularity)
        {
            return granularity switch
            {
                "week" => date.Date.AddDays(-(int)date.DayOfWeek),
                "month" => new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                _ => date.Date
            };
        }

        private static bool IsSameBucket(DateTime date, DateTime bucketStart, string granularity)
        {
            return BucketStart(date, granularity) == bucketStart;
        }

        private static string BuildCustomerName(User? customer)
        {
            if (customer == null)
            {
                return string.Empty;
            }

            var fullName = $"{customer.FirstName} {customer.LastName}".Trim();
            return string.IsNullOrWhiteSpace(fullName) ? customer.Username : fullName;
        }
    }
}
