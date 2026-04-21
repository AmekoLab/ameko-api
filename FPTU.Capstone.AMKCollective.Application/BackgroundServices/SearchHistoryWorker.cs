using FPTU.Capstone.AMKCollective.Application.DTOs.Search;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.BackgroundServices
{
    public class SearchHistoryWorker : BackgroundService
    {
        private readonly ISearchHistoryQueue _queue;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SearchHistoryWorker> _logger;

        // Gom tối đa 20 item để xử lý 1 lần (Fix 2)
        private const int BatchSize = 20;

        public SearchHistoryWorker(
            ISearchHistoryQueue queue,
            IServiceProvider serviceProvider,
            ILogger<SearchHistoryWorker> logger)
        {
            _queue = queue;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var batch = new List<SearchLogEvent>(BatchSize);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 1. Chờ cho đến khi có ít nhất 1 item được đẩy vào Queue
                    var first = await _queue.DequeueAsync(stoppingToken);
                    batch.Add(first);

                    // 2. Rút nhanh (Drain) thêm các item đang chờ sẵn (không dùng await để tránh chậm)
                    while (batch.Count < BatchSize && _queue.TryDequeue(out var extra))
                    {
                        batch.Add(extra);
                    }

                    // 3. Xử lý lưu DB cho cả mẻ
                    using var scope = _serviceProvider.CreateScope();
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                    foreach (var item in batch)
                    {
                        await unitOfWork.UserSearchHistories.LogSearchAsync(
                            item.UserId, item.Keyword, item.SearchType, stoppingToken);
                    }

                    // 4. lưu thật: Tốn 1 round-trip DB duy nhất cho tối đa 20 thao tác
                    await unitOfWork.CommitAsync();

                    batch.Clear();
                }
                catch (OperationCanceledException)
                {
                    // Tắt server an toàn (Graceful shutdown)
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi chạy Background Worker lưu lịch sử tìm kiếm.");
                    batch.Clear(); // Xóa mẻ lỗi, không làm treo luồng, tiếp tục vòng lặp mới
                }
            }
        }
    }
}
