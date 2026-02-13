using FPTU.Capstone.AMKCollective.Application.DTOs.Voucher;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IVoucherRepository
    {
        Task<Voucher?> GetByIdAsync(Guid id);
        Task<Voucher?> GetByCodeAsync(string code);
        Task<IEnumerable<Voucher>> GetByCreatorIdAsync(Guid creatorId);
        Task<IEnumerable<Voucher>> GetValidVouchersForUserAsync(Guid userId);

        Task AddAsync(Voucher voucher);
        void Update(Voucher voucher);
        void Delete(Voucher voucher);

        Task<bool> IsVoucherUsedAsync(Guid voucherId);
        Task<(IEnumerable<Voucher> Items, int TotalCount)> GetVouchersByFilterAsync(Guid creatorId, VoucherFilterRequest filter);
        Task<IEnumerable<Voucher>> GetPublicVouchersByShopAsync(Guid shopUserId);
    }
}
