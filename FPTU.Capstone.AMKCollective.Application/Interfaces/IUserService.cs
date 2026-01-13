using System.Collections.Generic;
using FPTU.Capstone.AMKCollective.Application.DTOs;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces
{
    public interface IUserService
    {
        IEnumerable<UserDto> GetAll();
    }
}
