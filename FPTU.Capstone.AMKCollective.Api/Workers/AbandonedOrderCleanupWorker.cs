using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;

namespace FPTU.Capstone.AMKCollective.API.Workers
{
    public class AbandonedOrderCleanupWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AbandonedOrderCleanupWorker> _logger;

        public AbandonedOrderCleanupWorker(IServiceProvider serviceProvider, ILogger<AbandonedOrderCleanupWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
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
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }
    }
}
