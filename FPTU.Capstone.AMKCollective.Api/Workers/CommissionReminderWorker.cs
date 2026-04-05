using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace FPTU.Capstone.AMKCollective.API.Workers
{
    /// <summary>
    /// Background worker that sends reminders and auto-cancels overdue commission requests.
    /// </summary>
    public class CommissionReminderWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CommissionReminderWorker> _logger;
        private readonly WorkerIntervals _workerIntervals;

        /// <summary>
        /// Initializes a new instance of the CommissionReminderWorker.
        /// </summary>
        public CommissionReminderWorker(IServiceProvider serviceProvider, ILogger<CommissionReminderWorker> logger, IOptions<WorkerIntervals> intervalOptions)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _workerIntervals = intervalOptions.Value;
        }

        /// <summary>
        /// Executes the reminder processing loop.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Commission Reminder Worker is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var commissionService = scope.ServiceProvider.GetRequiredService<ICommissionService>();
                        await commissionService.ProcessCommissionRemindersAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while processing commission reminders.");
                }

                await Task.Delay(TimeSpan.FromMinutes(_workerIntervals.CommissionReminderMinutes), stoppingToken);
            }
        }
    }
}
