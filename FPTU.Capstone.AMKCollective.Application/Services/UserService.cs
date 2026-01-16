using System.Collections.Generic;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.Interfaces;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    // Application service translates domain entities to DTOs and uses UnitOfWork
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;

        public UserService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IEnumerable<UserDto> GetAll()
        {
            var users = _unitOfWork.Users.GetAll();
            foreach (var u in users)
            {
                var fullName = $"{u.FirstName} {u.LastName}";
                yield return new UserDto(u.Id, u.Email, fullName);
            }
        }
    }
}
