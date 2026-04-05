using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IAssemblyProgressLogRepository
    {
        Task<IEnumerable<AssemblyProgressLog>> GetLogsByOrderItemIdAsync(Guid orderItemId);
        Task<AssemblyProgressLog?> GetByIdAsync(Guid id);
        Task AddAsync(AssemblyProgressLog log);
        Task AddRangeAsync(IEnumerable<AssemblyProgressLog> logs);
        void Update(AssemblyProgressLog log);
        void UpdateRange(IEnumerable<AssemblyProgressLog> logs);
        void Delete(AssemblyProgressLog log);
        Task<bool> HasLogsForOrderAsync(Guid orderId);
    }
}
