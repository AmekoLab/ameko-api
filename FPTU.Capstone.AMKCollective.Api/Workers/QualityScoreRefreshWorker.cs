using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Microsoft.Extensions.Options;

namespace FPTU.Capstone.AMKCollective.API.Workers
{
    public class QualityScoreRefreshWorker : BackgroundService
    {
        private readonly ILogger<QualityScoreRefreshWorker> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly QualityScoreSettings _settings; // Thêm biến lưu Settings

        // Inject IOptions<QualityScoreSettings> vào Constructor
        public QualityScoreRefreshWorker(
            ILogger<QualityScoreRefreshWorker> logger,
            IServiceScopeFactory scopeFactory,
            IOptions<QualityScoreSettings> options)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _settings = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Quality Score Refresh Worker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessQualityScoresAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while refreshing Quality Scores.");
                }

                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }

        private async Task ProcessQualityScoresAsync()
        {
            _logger.LogInformation($"Starting Quality Score calculation at {DateTime.UtcNow}");

            using var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var qualityScoreService = scope.ServiceProvider.GetRequiredService<IQualityScoreService>();

            // DÙNG _settings.TimeWindowDays TẠI ĐÂY
            var startDate = DateTime.UtcNow.AddDays(-_settings.TimeWindowDays);
            var endDate = DateTime.UtcNow;

            int pageNumber = 1;
            bool hasMoreShops = true;

            while (hasMoreShops)
            {
                _logger.LogInformation($"Processing batch {pageNumber} of active shops...");

                // DÙNG _settings.BatchSize TẠI ĐÂY
                var (activeShops, totalCount) = await unitOfWork.Shops.GetActiveShopsForUserAsync(
                    searchTerm: null,
                    pageNumber: pageNumber,
                    pageSize: _settings.BatchSize);

                if (activeShops == null || !activeShops.Any())
                {
                    break;
                }

                foreach (var shop in activeShops)
                {
                    try
                    {
                        var result = await qualityScoreService.CalculateShopScoreAsync(shop.Id, startDate, endDate);

                        var snapshot = new QualityScoreSnapshot
                        {
                            ShopId = shop.Id,
                            CapturedAt = endDate,
                            IssueRate = result.RawMetrics.IssueRate,
                            AvgResponseHours = result.RawMetrics.AvgResponseHours,
                            RefundRate = result.RawMetrics.RefundRate,
                            RepurchaseRate = result.RawMetrics.RepurchaseRate,
                            PositiveFeedbackRate = result.RawMetrics.PositiveFeedbackRate,
                            TotalScore = result.TotalScore,
                            Badge = result.Badge
                        };

                        await unitOfWork.QualityScoreSnapshots.AddAsync(snapshot);

                          // shop.CurrentQualityScore = result.TotalScore; // Comment lại để không đè Điểm Uy Tín của shop
                        shop.Badge = result.Badge;

                        await unitOfWork.Shops.UpdateAsync(shop);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to process Quality Score for Shop ID: {shop.Id}.");
                    }
                }

                await unitOfWork.CommitAsync();

                // DÙNG _settings.BatchSize TẠI ĐÂY ĐỂ TÍNH TOÁN VÒNG LẶP KẾ TIẾP
                if (pageNumber * _settings.BatchSize >= totalCount)
                {
                    hasMoreShops = false;
                }
                else
                {
                    pageNumber++;
                }
            }

            _logger.LogInformation("Finished Quality Score calculation.");
        }
    }
}