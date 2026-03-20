using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly ApplicationDbContext _context;

        public NotificationRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Notification?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            return await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id, ct);
        }

        public async Task<IEnumerable<Notification>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        {
            return await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task AddAsync(Notification notification, CancellationToken ct = default)
        {
            await _context.Notifications.AddAsync(notification, ct);
        }

        public void Update(Notification notification)
        {
            _context.Notifications.Update(notification);
        }

        public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
        {
            return await _context.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead, ct);
        }

        public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
        {
            var unread = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync(ct);

            foreach (var n in unread)
            {
                n.IsRead = true;
            }
        }

        public async Task<bool> IsSpamAsync(Guid receiverId, Guid actorId, FPTU.Capstone.AMKCollective.Domain.Enums.NotificationType type, string referenceId, string referenceType, DateTime timeWindow, CancellationToken ct = default)
        {
            return await _context.Notifications
                .AsNoTracking()
                .AnyAsync(n => 
                    n.UserId == receiverId && 
                    n.ActorId == actorId && 
                    n.Type == type && 
                    n.ReferenceId == referenceId && 
                    n.ReferenceType == referenceType &&
                    n.CreatedAt >= timeWindow, ct);
        }

        public async Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken ct = default)
        {
            await _context.Notifications.AddRangeAsync(notifications, ct);
        }

        public async Task<List<Notification>> GetCursorPagedAsync(Guid userId, string? referenceType, FPTU.Capstone.AMKCollective.Domain.Enums.NotificationType? type, DateTime? cursorDate, int? cursorId, int pageSize, CancellationToken ct = default)
        {
            var query = _context.Notifications.AsNoTracking().Where(n => n.UserId == userId);

            if (!string.IsNullOrEmpty(referenceType))
            {
                query = query.Where(n => n.ReferenceType == referenceType);
            }

            if (type.HasValue)
            {
                query = query.Where(n => n.Type == type.Value);
            }

            if (cursorDate.HasValue && cursorId.HasValue)
            {
                var cDate = cursorDate.Value;
                var cId = cursorId.Value;
                query = query.Where(n =>
                    n.CreatedAt < cDate ||
                    (n.CreatedAt == cDate && n.Id < cId)
                );
            }

            return await query
                .OrderByDescending(n => n.CreatedAt)
                .ThenByDescending(n => n.Id)
                .Take(pageSize + 1)
                .ToListAsync(ct);
        }

        public async Task<(IEnumerable<Notification> Items, int TotalCount)> GetAllPagedAsync(int pageNumber, int pageSize, CancellationToken ct = default)
        {
            var query = _context.Notifications.AsNoTracking();

            var totalCount = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, totalCount);
        }

        public void Remove(Notification notification)
        {
            _context.Notifications.Remove(notification);
        }
    }
}
