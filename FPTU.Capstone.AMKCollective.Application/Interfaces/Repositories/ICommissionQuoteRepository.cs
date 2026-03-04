using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface ICommissionQuoteRepository
    {
        Task<CommissionQuote?> GetByIdAsync(Guid id);
        Task<IEnumerable<CommissionQuote>> GetByRequestIdAsync(Guid requestId);
        Task<IEnumerable<CommissionQuote>> GetQuotesByShopIdAsync(Guid shopId);
        Task AddAsync(CommissionQuote quote);
        Task UpdateAsync(CommissionQuote quote);
    }
}
