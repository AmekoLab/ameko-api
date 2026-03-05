using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace FPTU.Capstone.AMKCollective.API.Workers
{
    /// <summary>
    /// Background worker that releases held funds from wallet after warranty period (30 days)
    /// </summary>
    public class FundsReleaseWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<FundsReleaseWorker> _logger;
        private readonly WorkerIntervals _workerIntervals;

        /// <summary>
        /// Initializes a new instance of the FundsReleaseWorker
        /// </summary>
        public FundsReleaseWorker(IServiceProvider serviceProvider, ILogger<FundsReleaseWorker> logger, IOptions<WorkerIntervals> intervalOptions)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _workerIntervals = intervalOptions.Value;
        }

        /// <summary>
        /// Executes the background service task
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Funds Release Worker is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
                        
                        _logger.LogInformation("Running funds release check...");
                        await orderService.ReleaseFundsForEligibleOrdersAsync(stoppingToken);
                        _logger.LogInformation("Funds release check completed.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Funds Release Worker.");
                }

                // Chạy 1 lần mỗi giờ (hoặc mỗi ngày tùy nhu cầu)
                await Task.Delay(TimeSpan.FromHours(_workerIntervals.FundsReleaseHours), stoppingToken);
            }
        }
    }
}