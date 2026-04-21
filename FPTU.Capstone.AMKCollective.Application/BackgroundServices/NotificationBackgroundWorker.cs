using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using System.Linq;

namespace FPTU.Capstone.AMKCollective.Application.BackgroundServices;

public class NotificationDispatchItem
{
    public Guid ActorId { get; set; }
    public Guid? ReceiverId { get; set; }
    public FPTU.Capstone.AMKCollective.Domain.Enums.NotificationType Type { get; set; }
    public string ReferenceId { get; set; } = string.Empty;
    public string ReferenceType { get; set; } = string.Empty;
    public string? RedirectUrl { get; set; }
}

public interface INotificationQueue
{
    ValueTask QueueNotificationAsync(NotificationDispatchItem item);
    ValueTask<NotificationDispatchItem> DequeueAsync(CancellationToken cancellationToken);
}

public class NotificationQueue : INotificationQueue
{
    private readonly Channel<NotificationDispatchItem> _queue;

    public NotificationQueue(int capacity = 10000)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _queue = Channel.CreateBounded<NotificationDispatchItem>(options);
    }

    public async ValueTask QueueNotificationAsync(NotificationDispatchItem item)
    {
        await _queue.Writer.WriteAsync(item);
    }

    public async ValueTask<NotificationDispatchItem> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}

public class NotificationBackgroundWorker : BackgroundService
{
    private readonly INotificationQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationBackgroundWorker> _logger;

    public NotificationBackgroundWorker(INotificationQueue queue, IServiceProvider serviceProvider, ILogger<NotificationBackgroundWorker> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var item = await _queue.DequeueAsync(stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                //var follows = await unitOfWork.Follows.GetByFollowedId(item.ActorId);
                //var followerIds = follows.Select(f => f.FollowerId).ToList();
                if (item.ReceiverId.HasValue)
                {
                    await notificationService.CreateNotificationAsync(
                        item.ReceiverId.Value, item.ActorId, item.Type, item.ReferenceId, item.ReferenceType, item.RedirectUrl, stoppingToken);
                }
                else
                {
                    var follows = await unitOfWork.Follows.GetByFollowedId(item.ActorId);
                    var followerIds = follows.Select(f => f.FollowerId).ToList();

                    if (followerIds.Any())
                    {
                        await notificationService.CreateBulkNotificationsAsync(
                            followerIds, item.ActorId, item.Type, item.ReferenceId, item.ReferenceType, item.RedirectUrl, stoppingToken);
                    }
                }

                //if (followerIds.Any())
                //{
                //    await notificationService.CreateBulkNotificationsAsync(
                //        followerIds, item.ActorId, item.Type, item.ReferenceId, item.ReferenceType, item.RedirectUrl, stoppingToken);
                //}
            }
            catch (OperationCanceledException)
            {
                // Graceful cancellation shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process background notification dispatch.");
            }
        }
    }
}
