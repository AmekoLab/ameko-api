using System.Collections.Generic;
using FPTU.Capstone.AMKCollective.Application.Interfaces;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Infrastructure.Services
{
    // Placeholder MySql repository implementation kept so the file exists.
    // When you enable the MySql driver / Dapper, replace this with the real implementation.
    public class MySqlUserRepository : IUserRepository
    {
        public IEnumerable<User> GetAll()
        {
            // Return empty list as placeholder
            return new List<User>();
        }
    }
}
