using System;
using System.Threading.Tasks;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using Microsoft.Extensions.Caching.Memory;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    public class RateLimitService : IRateLimitService
    {
        private readonly IMemoryCache _cache;

        public RateLimitService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task<bool> IsAllowedAsync(Guid userId, string actionType, int limitPerMinute)
        {
            var cacheKey = $"RateLimit_{actionType}_{userId}_{DateTime.UtcNow:yyyyMMddHHmm}";
            
            if (_cache.TryGetValue(cacheKey, out int count))
            {
                if (count >= limitPerMinute)
                {
                    return false;
                }
                _cache.Set(cacheKey, count + 1, TimeSpan.FromMinutes(2));
            }
            else
            {
                _cache.Set(cacheKey, 1, TimeSpan.FromMinutes(2));
            }

            return true;
        }
    }
}
