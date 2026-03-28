using System;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface IRateLimitService
    {
        Task<bool> IsAllowedAsync(Guid userId, string actionType, int limitPerMinute);
    }
}
