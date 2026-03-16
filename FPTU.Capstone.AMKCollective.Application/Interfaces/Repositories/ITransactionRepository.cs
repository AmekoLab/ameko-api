using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface ITransactionRepository
    {
        Task AddAsync(Transaction transaction);
        Task<List<Transaction>> GetByWalletIdAsync(Guid walletId);
        Task<(List<Transaction> Items, int TotalCount)> GetTransactionsByFilterAsync(PaymentFilterRequest filter);
    }
}
