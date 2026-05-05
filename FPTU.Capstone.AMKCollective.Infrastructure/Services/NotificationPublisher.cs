using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Application.DTOs.Community;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class NotificationPublisher : INotificationPublisher
    {
        private readonly IHubContext<RealTimeHub> _hubContext;
        private readonly ILogger<NotificationPublisher> _logger;
        private readonly ConnectionMapping<Guid> _connections = new();

        public NotificationPublisher(IHubContext<RealTimeHub> hubContext, ILogger<NotificationPublisher> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task PublishBroadcastAsync(NotificationDto notification, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Broadcasting real-time notification to all connected users. Type: {Type}", notification.Type);
            await _hubContext.Clients.All.SendAsync("notificationReceived", notification, cancellationToken);
        }

        public async Task PublishNotificationAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Publishing real-time notification to user {UserId}. Type: {Type}", userId, notification.Type);

            // 1. Try default mechanism
            await _hubContext.Clients.User(userId.ToString()).SendAsync("notificationReceived", notification, cancellationToken);

            // 2. Fallback to explicit connection IDs tracking (because IUserIdProvider might be broken for 'nameid' claim)
            var connectionIds = _connections.GetConnections(userId).ToList();
            if (connectionIds.Any())
            {
                _logger.LogInformation("Found {Count} active connections for user {UserId}. Sending via Clients.Clients()", connectionIds.Count, userId);
                await _hubContext.Clients.Clients(connectionIds).SendAsync("notificationReceived", notification, cancellationToken);
            }
            else
            {
                _logger.LogWarning("No active connections found in ConnectionMapping for user {UserId}", userId);
            }
        }
    }
}
