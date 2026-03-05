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
    }
}
