using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace FPTU.Capstone.AMKCollective.API.Workers
{
    public class AbandonedOrderCleanupWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AbandonedOrderCleanupWorker> _logger;
        private readonly WorkerIntervals _workerIntervals;

        public AbandonedOrderCleanupWorker(IServiceProvider serviceProvider, ILogger<AbandonedOrderCleanupWorker> logger, IOptions<WorkerIntervals> intervalOptions)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _workerIntervals = intervalOptions.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Abandoned Order Cleanup Worker is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();

                        _logger.LogInformation("Scanning for abandoned unpaid orders...");
                        await orderService.CancelAbandonedOrdersAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while cleaning up abandoned orders.");
                }
                await Task.Delay(TimeSpan.FromMinutes(_workerIntervals.AbandonedOrderCleanupMinutes), stoppingToken);
            }
        }
    }
}
