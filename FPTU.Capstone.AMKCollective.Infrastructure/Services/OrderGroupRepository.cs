using FPTU.Capstone.AMKCollective.Application.DTOs.Order;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class OrderGroupRepository: IOrderGroupRepository
    {
        private readonly ApplicationDbContext _context;

        public OrderGroupRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<OrderGroup?> GetByIdAsync(Guid id, CancellationToken token = default)
        {
            return await _context.OrderGroups
                .Include(og => og.Orders)
                    .ThenInclude(o => o.OrderItems)
                        .ThenInclude(oi => oi.OrderItemComponents)
                .Include(og => og.Orders)
                    .ThenInclude(o => o.OrderIssues)
                .AsSplitQuery()
                .FirstOrDefaultAsync(og => og.Id == id && !og.IsDeleted, token);
        }

        public async Task CreateAsync(OrderGroup orderGroup, CancellationToken token = default)
        {
            await _context.OrderGroups.AddAsync(orderGroup, token);
        }

        public async Task UpdatePaymentStatusAsync(Guid orderGroupId, PaymentStatus status, CancellationToken token = default)
        {
            await _context.OrderGroups
                .Where(og => og.Id == orderGroupId)
                .ExecuteUpdateAsync(s => s.SetProperty(og => og.PaymentStatus, status), token);
        }

        public async Task<(IEnumerable<OrderGroup> groups, int totalCount)> GetByUserIdPagedAsync(Guid userId, MyPaymentHistoryFilterRequest filter, CancellationToken token = default)
        {
            var query = _context.OrderGroups
                .AsNoTracking()
                .Include(og => og.Orders)
                    .ThenInclude(o => o.Shop)
                .Include(og => og.Orders)
                    .ThenInclude(o => o.OrderIssues)
                .Include(og => og.Orders)
                    .ThenInclude(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                .Include(og => og.Orders)
                    .ThenInclude(o => o.OrderItems)
                        .ThenInclude(oi => oi.OrderItemComponents)
                .Include(og => og.Payments)
                .Where(og => og.Orders.Any(o => o.CustomerId == userId) && !og.IsDeleted)
                .AsQueryable();

            if (!string.IsNullOrEmpty(filter.PaymentStatus) && Enum.TryParse<PaymentStatus>(filter.PaymentStatus, true, out var parsedStatus))
                query = query.Where(og => og.PaymentStatus == parsedStatus);

            if (!string.IsNullOrEmpty(filter.PaymentMethod) && Enum.TryParse<PaymentMethod>(filter.PaymentMethod, true, out var parsedMethod))
                query = query.Where(og => og.Payments.Any(p => p.Method == parsedMethod));

            if (filter.FromDate.HasValue)
                query = query.Where(og => og.CreatedAt >= filter.FromDate.Value.ToUniversalTime());

            if (filter.ToDate.HasValue)
                query = query.Where(og => og.CreatedAt <= filter.ToDate.Value.ToUniversalTime());

            var totalCount = await query.CountAsync(token);
            var groups = await query
                .OrderByDescending(og => og.CreatedAt)
                .Skip((filter.Page - 1) * filter.Size)
                .Take(filter.Size)
                .AsSplitQuery()
                .ToListAsync(token);

            return (groups, totalCount);
        }
        public async Task<int> SaveChangesAsync(CancellationToken token = default)
        {
            return await _context.SaveChangesAsync(token);
        }
        public async Task UpdateAsync(OrderGroup orderGroup)
        {
            _context.OrderGroups.Update(orderGroup);
            await Task.CompletedTask;
        }
        public void Delete(OrderGroup orderGroup)
        {
            _context.OrderGroups.Remove(orderGroup);
        }
    }
}
