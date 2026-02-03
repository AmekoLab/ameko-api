using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Application.Interfaces.Services
{
    public interface ITokenService
    {
        string CreateToken(User user);
    }
}
