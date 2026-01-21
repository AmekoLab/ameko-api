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
            CreateMap<User, LoginResponse>()
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.Name.ToString()));
            
            CreateMap<RegisterRequest, User>()
                .ForMember(dest => dest.HashedPassword, opt => opt.Ignore())
                .ForMember(dest => dest.Role, opt => opt.Ignore());

            CreateMap<CreateUserRequest, User>()
                .ForMember(dest => dest.HashedPassword, opt => opt.Ignore())
                .ForMember(dest => dest.Role, opt => opt.Ignore());

            CreateMap<UpdateUserAdminRequest, User>()
                .ForMember(dest => dest.Role, opt => opt.Ignore());

            CreateMap<UpdateProfileRequest, User>();

            // TODO: Thêm mapping cho các entities khác ở đây
            // Ví dụ:
            // CreateMap<Order, OrderDto>();
            // CreateMap<Product, ProductDto>();

            //==================MODEL=======================//
            CreateMap<Model, PartDto>()
                .ForMember(dest => dest.ShopName, opt => opt.MapFrom(src => src.Shop.ShopName))
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name));

            CreateMap<CreateUpdatePartDto, Model>()
                .ForMember(dest => dest.ThumbnailURL, opt => opt.Ignore())
                .ForMember(dest => dest.DefaultLayerImageUrl, opt => opt.Ignore())
                .ForMember(dest => dest.Slug, opt => opt.Ignore());

            //==================MODEL=======================//

            //==================KITDESIGN=======================//

            CreateMap<KitDesignOption, CompatiblePartDto>()
                .ForMember(dest => dest.PartId, opt => opt.MapFrom(src => src.ComponentId))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Component.Name))
                .ForMember(dest => dest.Price, opt => opt.MapFrom(src => src.Component.Price))
                .ForMember(dest => dest.ThumbnailUrl, opt => opt.MapFrom(src => src.Component.ThumbnailURL))
                .ForMember(dest => dest.IsDefault, opt => opt.MapFrom(src => src.IsDefault))
                .ForMember(dest => dest.LayerImageUrl, opt => opt.MapFrom(src =>
                    !string.IsNullOrEmpty(src.LayerImageUrl)
                        ? src.LayerImageUrl
                        : src.Component.DefaultLayerImageUrl));

            CreateMap<CreateKitOptionDto, KitDesignOption>();
            //==================KITDESIGN=======================//

        }
    }
}
