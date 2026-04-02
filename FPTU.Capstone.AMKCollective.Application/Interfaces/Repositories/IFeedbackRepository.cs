using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IFeedbackRepository
    {
        Task<Feedback?> GetByIdAsync(Guid feedbackId);
        Task<Feedback?> GetByOrderIdAsync(Guid orderId);
        Task<(IEnumerable<Feedback> Items, int TotalCount)> GetFeedbacksByShopIdAsync(Guid shopId, int pageNumber, int pageSize);
        Task AddAsync(Feedback feedback);
        void Update(Feedback feedback);
        Task<bool> ExistsByOrderIdAsync(Guid orderId);
    }
}
