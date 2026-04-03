using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using Microsoft.Extensions.Options;

namespace FPTU.Capstone.AMKCollective.API.Workers
{
    /// <summary>
    /// Background worker that removes stale accounts still pending email verification.
    /// </summary>
    public class UnverifiedAccountCleanupWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UnverifiedAccountCleanupWorker> _logger;
        private readonly WorkerIntervals _workerIntervals;
        private readonly SecuritySettings _securitySettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnverifiedAccountCleanupWorker"/> class.
        /// </summary>
        public UnverifiedAccountCleanupWorker(
            IServiceProvider serviceProvider,
            ILogger<UnverifiedAccountCleanupWorker> logger,
            IOptions<WorkerIntervals> intervalOptions,
            IOptions<SecuritySettings> securityOptions)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _workerIntervals = intervalOptions.Value;
            _securitySettings = securityOptions.Value;
        }

        /// <summary>
        /// Executes periodic cleanup of unverified accounts older than the configured threshold.
        /// </summary>
        /// <param name="stoppingToken">Cancellation token used to stop the background loop.</param>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Unverified Account Cleanup Worker is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                    var thresholdUtc = DateTime.UtcNow.AddHours(-_securitySettings.UnverifiedAccountDeletionHours);
                    var deletedCount = await unitOfWork.Users.DeleteUnverifiedAccountsOlderThanAsync(thresholdUtc, stoppingToken);

                    if (deletedCount > 0)
                    {
                        await unitOfWork.CommitAsync();
                        _logger.LogInformation("Deleted {DeletedCount} unverified accounts older than {Hours}h.", deletedCount, _securitySettings.UnverifiedAccountDeletionHours);
                    }
                    else
                    {
                        _logger.LogInformation("No unverified accounts eligible for cleanup.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while cleaning up unverified accounts.");
                }

                await Task.Delay(TimeSpan.FromMinutes(_workerIntervals.UnverifiedAccountCleanupMinutes), stoppingToken);
            }
        }
    }
}
