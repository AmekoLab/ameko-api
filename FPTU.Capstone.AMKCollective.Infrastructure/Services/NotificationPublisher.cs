using System;
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

        public NotificationPublisher(IHubContext<RealTimeHub> hubContext, ILogger<NotificationPublisher> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task PublishNotificationAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Publishing real-time notification to user {UserId}. Type: {Type}", userId, notification.Type);
            await _hubContext.Clients.User(userId.ToString()).SendAsync("notificationReceived", notification, cancellationToken);
        }
    }
}
