using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;


namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class AssemblyStepTemplateRepository : IAssemblyStepTemplateRepository
    {
        private readonly ApplicationDbContext _context;

        public AssemblyStepTemplateRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<AssemblyStepTemplate>> GetTemplatesByShopIdAsync(Guid shopId)
        {
            return await _context.AssemblyStepTemplates
                                 .Where(x => x.ShopId == shopId)
                                 .OrderBy(x => x.StepOrder)
                                 .ToListAsync();
        }

        public async Task<AssemblyStepTemplate?> GetByIdAsync(Guid id)
        {
            return await _context.AssemblyStepTemplates.FindAsync(id);
        }

        public async Task AddAsync(AssemblyStepTemplate template)
        {
            await _context.AssemblyStepTemplates.AddAsync(template);
        }

        public void Update(AssemblyStepTemplate template)
        {
            _context.AssemblyStepTemplates.Update(template);
        }

        public void Delete(AssemblyStepTemplate template)
        {
            _context.AssemblyStepTemplates.Remove(template);
        }
    }
}