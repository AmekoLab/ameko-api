using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IAssembledProductFeedbackRepository
    {
        Task<AssembledProductFeedback?> GetByIdAsync(Guid feedbackId);
        Task<(IEnumerable<AssembledProductFeedback> Items, int TotalCount)> GetByProductIdAsync(Guid productId, int pageNumber, int pageSize);
        Task<(IEnumerable<AssembledProductFeedback> Items, int TotalCount)> GetByShopIdAsync(Guid shopId, int pageNumber, int pageSize);
        Task AddAsync(AssembledProductFeedback feedback);
        void Update(AssembledProductFeedback feedback);
        Task<bool> ExistsByOrderItemIdAsync(Guid orderItemId);
    }
}
