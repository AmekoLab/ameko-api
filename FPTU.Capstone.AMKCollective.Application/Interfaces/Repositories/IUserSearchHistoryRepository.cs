using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IUserSearchHistoryRepository
    {
        // Hàm ghi log
        Task LogSearchAsync(Guid userId, string keyword, string searchType, CancellationToken cancellationToken = default);

        // Hàm lấy top từ khoá cho thuật toán Feed
        Task<List<UserSearchHistory>> GetTopSearchesByUserAsync(Guid userId, int limit = 10, CancellationToken cancellationToken = default);
    }
}
