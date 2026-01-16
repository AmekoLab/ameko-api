using System.Collections.Generic;
using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.Interfaces;

namespace FPTU.Capstone.AMKCollective.Application.Services
{
    // Application service translates domain entities to DTOs and uses UnitOfWork
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public UserService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public IEnumerable<UserDto> GetAll()
        {
            var users = _unitOfWork.Users.GetAll();
            // Sử dụng AutoMapper để map từ Entity sang DTO
            return _mapper.Map<IEnumerable<UserDto>>(users);
        }
    }
}
