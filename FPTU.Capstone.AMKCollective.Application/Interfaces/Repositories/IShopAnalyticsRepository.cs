using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IShopAnalyticsRepository
    {
        /// Lấy 5 chỉ số thô của Shop trong một khoảng thời gian nhất định (Cửa sổ thời gian)
        Task<ShopMetricsDto> GetMetricsAsync(Guid shopId, DateTime startDate, DateTime endDate);
    }
}
