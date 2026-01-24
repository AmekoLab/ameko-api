using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Auth;
using FPTU.Capstone.AMKCollective.Application.DTOs.Follow;
using FPTU.Capstone.AMKCollective.Application.DTOs.User;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using System.Text.Json;

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

            //==================FOLLOW=======================//
            CreateMap<FollowRequest, Follow>();
            CreateMap<Follow, FollowResponse>();

            // TODO: Thêm mapping cho các entities khác ở đây

            //==================MODEL=======================//
            CreateMap<Model, PartDto>()
                .ForMember(dest => dest.ShopName, opt => opt.MapFrom(src => src.Shop.ShopName))
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name));

            CreateMap<CreateUpdatePartRequest, Model>()
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

            CreateMap<CreateKitOptionRequest, KitDesignOption>()
                .ForMember(dest => dest.LayerImageUrl, opt => opt.Ignore()); // Handled in service
            //==================KITDESIGN=======================//

           
            //==================ShopProfile=====================//
            CreateMap<ShopProfile, ShopDto>();

            CreateMap<ShopProfile, ShopDetailDto>();

            CreateMap<CreateShopRequest, ShopProfile>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())       
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore()) 
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UserId, opt => opt.Ignore())    
                .ForMember(dest => dest.Status, opt => opt.Ignore())    
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())  
                                                                        
                .ForMember(dest => dest.LogoUrl, opt => opt.Ignore())
                .ForMember(dest => dest.BannerUrl, opt => opt.Ignore());
            CreateMap<UpdateShopRequest, ShopProfile>()
                .ForMember(dest => dest.LogoUrl, opt => opt.Ignore())
                .ForMember(dest => dest.BannerUrl, opt => opt.Ignore())

                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            //==================ShopProfile=====================//


            // =========================================================
            // 1. ORDER GROUP (ORDER GROUP -> DTO)
            // =========================================================
            CreateMap<OrderGroup, OrderGroupDto>()
                .ForMember(dest => dest.PaymentStatus, opt => opt.MapFrom(src => src.PaymentStatus));

            // =========================================================
            // 2. ORDER (ORDER -> DTO)
            // =========================================================
            CreateMap<Order, OrderDto>()
    // [FIX LỖI NULL]: Vì Giỏ hàng (InCart) chưa có ShopId cụ thể, nên src.Shop sẽ bị null.
    // Nếu không check null, dòng này sẽ gây crash API GetCart.
                .ForMember(dest => dest.ShopName, opt => opt.MapFrom(src => src.Shop != null ? src.Shop.ShopName : "N/A"))

                .ForMember(dest => dest.OrderStatus, opt => opt.MapFrom(src => src.OrderStatus))
                .ForMember(dest => dest.SubTotal, opt => opt.MapFrom(src => src.SubTotal))
                .ForMember(dest => dest.ShippingFee, opt => opt.MapFrom(src => src.ShippingFee))
                .ForMember(dest => dest.DiscountAmount, opt => opt.MapFrom(src => src.DiscountAmount))
                .ForMember(dest => dest.TotalAmount, opt => opt.MapFrom(src => src.TotalAmount))
                //tự map OrderItems nếu tên trùng nhau
                .ForMember(dest => dest.OrderItems, opt => opt.MapFrom(src => src.OrderItems));

            // =========================================================
            // 3. ORDER ITEM (ORDER ITEM -> DTO)
            // =========================================================
            CreateMap<OrderItem, OrderItemDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
               .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId))
               .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : src.ProductName))
               .ForMember(dest => dest.ProductImage, opt => opt.MapFrom(src => src.Product != null ? src.Product.ThumbnailURL : src.ProductImage))

               .ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => src.UnitPrice))
               .ForMember(dest => dest.TotalPrice, opt => opt.MapFrom(src => src.TotalPrice))
               .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => src.Quantity))

      
               .ForMember(dest => dest.CustomComponentIds, opt => opt.MapFrom(src =>
                   !string.IsNullOrEmpty(src.DesignConfig) && src.DesignConfig.Trim().StartsWith("[")
                   ? JsonSerializer.Deserialize<List<Guid>>(src.DesignConfig, (JsonSerializerOptions?)null)
                   : null)) 

               .ForMember(dest => dest.IsCustom, opt => opt.MapFrom(src => src.IsCustom));

            //order item component
            CreateMap<OrderItemComponent, OrderItemComponentDto>();

        }

    }
    
}
