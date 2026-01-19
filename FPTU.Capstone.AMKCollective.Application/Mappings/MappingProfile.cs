using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Auth;
using FPTU.Capstone.AMKCollective.Application.DTOs.User;
using FPTU.Capstone.AMKCollective.Domain.Entities;

namespace FPTU.Capstone.AMKCollective.Application.Mappings
{
    /// <summary>
    /// Main mapping profile - Thêm các mapping mới vào đây
    /// </summary>
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // User mappings
            CreateMap<User, UserDto>()
                .ForMember(dest => dest.FullName, 
                    opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName}"));
            
            CreateMap<UserDto, User>()
                .ForMember(dest => dest.FirstName, 
                    opt => opt.MapFrom(src => src.FullName.Split(new[] { ' ' })[0]))
                .ForMember(dest => dest.LastName, 
                    opt => opt.MapFrom(src => string.Join(" ", src.FullName.Split(new[] { ' ' }).Skip(1))));

            CreateMap<User, UserProfileDto>();
            CreateMap<User, LoginResponse>();

            // TODO: Thêm mapping cho các entities khác ở đây
            // Ví dụ:
            // CreateMap<Order, OrderDto>();
            // CreateMap<Product, ProductDto>();
        }
    }
}
