using System.Collections.Generic;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces
{
    public interface IUserRepository
    {
        IEnumerable<User> GetAll();
    }
}
