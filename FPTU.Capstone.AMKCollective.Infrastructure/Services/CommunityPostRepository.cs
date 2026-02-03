using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    public class CommunityPostRepository
    {
        private readonly ApplicationDbContext _context;
        public CommunityPostRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<IEnumerable<object>> GetCommunityPostsAsync()
        {
            throw new NotImplementedException();
        }
    }
}
