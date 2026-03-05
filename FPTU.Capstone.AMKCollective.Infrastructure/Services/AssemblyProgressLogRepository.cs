using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using System;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class AssemblyProgressLogRepository : IAssemblyProgressLogRepository
    {
        private readonly ApplicationDbContext _context;

        public AssemblyProgressLogRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<AssemblyProgressLog>> GetLogsByOrderItemIdAsync(Guid orderItemId)
        {
            return await _context.AssemblyProgressLogs
                                 .Where(x => x.OrderItemId == orderItemId)
                                 .OrderBy(x => x.StepOrder)
                                 .ToListAsync();
        }

        public async Task<AssemblyProgressLog?> GetByIdAsync(Guid id)
        {
            return await _context.AssemblyProgressLogs.FindAsync(id);
        }

        public async Task AddAsync(AssemblyProgressLog log)
        {
            await _context.AssemblyProgressLogs.AddAsync(log);
        }

        public async Task AddRangeAsync(IEnumerable<AssemblyProgressLog> logs)
        {
            await _context.AssemblyProgressLogs.AddRangeAsync(logs);
        }

        public void Update(AssemblyProgressLog log)
        {
            _context.AssemblyProgressLogs.Update(log);
        }
    }
}
