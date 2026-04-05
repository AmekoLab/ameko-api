using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace FPTU.Capstone.AMKCollective.API.Workers
{
    /// <summary>
    /// Background worker that auto-cancels orders if assembly tracking is not initialized in time.
    /// </summary>
    public class AssemblyTrackingTimeoutWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AssemblyTrackingTimeoutWorker> _logger;
        private readonly WorkerIntervals _workerIntervals;

        /// <summary>
        /// Initializes a new instance of the AssemblyTrackingTimeoutWorker.
        /// </summary>
        public AssemblyTrackingTimeoutWorker(IServiceProvider serviceProvider, ILogger<AssemblyTrackingTimeoutWorker> logger, IOptions<WorkerIntervals> intervalOptions)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _workerIntervals = intervalOptions.Value;
        }

        /// <summary>
        /// Executes the background loop to cancel overdue orders.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Assembly tracking timeout worker is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
                        await orderService.AutoCancelOrdersWithoutAssemblyAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while processing assembly tracking timeouts.");
                }

                await Task.Delay(TimeSpan.FromMinutes(_workerIntervals.AssemblyTrackingTimeoutMinutes), stoppingToken);
            }
        }
    }
}
