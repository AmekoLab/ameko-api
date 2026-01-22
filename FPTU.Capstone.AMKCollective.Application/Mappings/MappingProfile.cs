using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Auth;
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
            CreateMap<User, UserDto>()
                .ForMember(dest => dest.FullName, 
                    opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName}"))
                .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.Name.ToString()));
            
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

            //==================Cart============================//
            CreateMap<Cart, CartDto>()
                .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.CartItems))
                .ForMember(dest => dest.TotalAmount, opt => opt.MapFrom(src =>
                    src.CartItems.Sum(i => (i.NegotiatedPrice ?? i.UnitPrice) * i.Quantity)));

            CreateMap<CartItem, CartItemDto>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name)) 
                .ForMember(dest => dest.ProductImage, opt => opt.MapFrom(src => src.Product.DefaultLayerImageUrl)) 
                .ForMember(dest => dest.ProductType, opt => opt.MapFrom(src => src.Product.PartType))
                .ForMember(dest => dest.CustomComponentIds, opt => opt.MapFrom(src =>
                    string.IsNullOrEmpty(src.CustomConfig)
                    ? null
                    : JsonSerializer.Deserialize<List<Guid>>(src.CustomConfig, (JsonSerializerOptions?)null))); // Parse JSON string -> List<Guid>


            //==================Cart============================//
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
            CreateMap<UpdateShopProfileRequest, ShopProfile>()
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
                .ForMember(dest => dest.ShopName, opt => opt.MapFrom(src => src.Shop.ShopName))
                .ForMember(dest => dest.OrderStatus, opt => opt.MapFrom(src => src.OrderStatus))

                .ForMember(dest => dest.SubTotal, opt => opt.MapFrom(src => src.SubTotal))
                .ForMember(dest => dest.ShippingFee, opt => opt.MapFrom(src => src.ShippingFee))
                .ForMember(dest => dest.DiscountAmount, opt => opt.MapFrom(src => src.DiscountAmount)) // DÒNG QUAN TRỌNG
                .ForMember(dest => dest.TotalAmount, opt => opt.MapFrom(src => src.TotalAmount));

            // =========================================================
            // 3. ORDER ITEM (ORDER ITEM -> DTO)
            // =========================================================
            CreateMap<OrderItem, OrderItemDto>()
                .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId))
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : src.ProductName))
                .ForMember(dest => dest.ProductImage, opt => opt.MapFrom(src => src.Product != null ? src.Product.ThumbnailURL : src.ProductImage))

                .ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => src.UnitPrice))
                .ForMember(dest => dest.TotalPrice, opt => opt.MapFrom(src => src.TotalPrice))
                .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => src.Quantity))

                .ForMember(dest => dest.CustomComponentIds, opt => opt.MapFrom(src =>
                    string.IsNullOrEmpty(src.DesignConfig)
                    ? null
                    : JsonSerializer.Deserialize<List<Guid>>(src.DesignConfig, (JsonSerializerOptions?)null)))

                .ForMember(dest => dest.IsCustom, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.DesignConfig)));



        }

    }
    
}
