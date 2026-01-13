using System.Collections.Generic;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.Interfaces;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    // Application service translates domain entities to DTOs and uses repository
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public IEnumerable<UserDto> GetAll()
        {
            var users = _userRepository.GetAll();
            foreach (var u in users)
            {
                yield return new UserDto(u.Id, u.Email, u.FullName);
            }
        }
    }
}
