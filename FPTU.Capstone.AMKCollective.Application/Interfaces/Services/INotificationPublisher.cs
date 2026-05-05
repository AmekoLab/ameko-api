using System;
using System.Threading;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Application.DTOs.Community;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface INotificationPublisher
    {
        Task PublishNotificationAsync(Guid userId, NotificationDto notification, CancellationToken cancellationToken = default);
        Task PublishBroadcastAsync(NotificationDto notification, CancellationToken cancellationToken = default);
    }
}
