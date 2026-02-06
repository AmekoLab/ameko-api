using FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Enums;

namespace FPTU.Capstone.AMKCollective.API.Workers
{
    public class OrderCancellationTimeoutWorker :BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OrderCancellationTimeoutWorker> _logger;

        public OrderCancellationTimeoutWorker(IServiceProvider serviceProvider, ILogger<OrderCancellationTimeoutWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Order Cancellation Timeout Worker is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Scanning for expired cancellation requests...");

                try
                {
                    // Tạo một Scope mới cho mỗi lần chạy (giống như 1 request HTTP)
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
                        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                        // 1. Tìm các Issue quá hạn (Logic này cần thêm vào Repo)
                        // Lấy các đơn InProgress tạo cách đây hơn 24h
                        var timeoutThreshold = DateTime.UtcNow.AddHours(-24);

                        // Cần thêm hàm này vào IOrderIssueRepository:
                        // Task<List<OrderIssue>> GetExpiredIssuesAsync(DateTime threshold);
                        var expiredIssues = await unitOfWork.OrderIssues.GetExpiredIssuesAsync(timeoutThreshold);

                        if (expiredIssues != null && expiredIssues.Count > 0)
                        {
                            _logger.LogInformation($"Found {expiredIssues.Count} expired issues.");

                            foreach (var issue in expiredIssues)
                            {
                                try
                                {
                                    // 2. Tự động hủy
                                    var request = new ProcessIssueRequest
                                    {
                                        IssueId = issue.Id,
                                        Decision = OrderIssueStatus.Cancelled,
                                        ShopResponse = "System Auto-Resolve: Shop did not respond within 24 hours."
                                    };

                                    // Gọi Service xử lý (Truyền Guid.Empty vì là System chạy)
                                    // Lưu ý: Cần đảm bảo hàm ProcessCancelRequestAsync xử lý được shopId = Guid.Empty
                                    await orderService.ProcessCancelRequestAsync(Guid.Empty, request);

                                    _logger.LogInformation($"Auto-cancelled issue {issue.Id} success.");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, $"Failed to auto-cancel issue {issue.Id}");
                                }
                            }
                        }
                        else
                        {
                            _logger.LogInformation("No expired issues found.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while processing timeout cancellations.");
                }

                // Chờ 30 phút (hoặc 1 tiếng) quét 1 lần. Không nên quét quá nhanh.
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }
    }
}
   