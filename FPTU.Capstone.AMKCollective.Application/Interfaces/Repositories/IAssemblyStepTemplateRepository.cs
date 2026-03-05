using FPTU.Capstone.AMKCollective.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories
{
    public interface IAssemblyStepTemplateRepository
    {
        Task<IEnumerable<AssemblyStepTemplate>> GetTemplatesByShopIdAsync(Guid shopId);
        Task<AssemblyStepTemplate?> GetByIdAsync(Guid id);
        Task AddAsync(AssemblyStepTemplate template);
        void Update(AssemblyStepTemplate template);
        void Delete(AssemblyStepTemplate template);
    }
}
