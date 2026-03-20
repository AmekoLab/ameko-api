using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface INotificationRepository
    {
        Task<Notification?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<IEnumerable<Notification>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
        Task AddAsync(Notification notification, CancellationToken ct = default);
        void Update(Notification notification);
        Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
        Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);
        Task<bool> IsSpamAsync(Guid receiverId, Guid actorId, FPTU.Capstone.AMKCollective.Domain.Enums.NotificationType type, string referenceId, string referenceType, DateTime timeWindow, CancellationToken ct = default);
        Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken ct = default);
        Task<List<Notification>> GetCursorPagedAsync(Guid userId, string? referenceType, FPTU.Capstone.AMKCollective.Domain.Enums.NotificationType? type, DateTime? cursorDate, int? cursorId, int pageSize, CancellationToken ct = default);
        Task<(IEnumerable<Notification> Items, int TotalCount)> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default);
        void Remove(Notification notification);
    }
}
