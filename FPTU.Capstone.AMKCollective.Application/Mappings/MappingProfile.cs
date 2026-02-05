using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.Auth;
using FPTU.Capstone.AMKCollective.Application.DTOs.Follow;
using FPTU.Capstone.AMKCollective.Application.DTOs.User;
using FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProduct;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
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
            CreateMap<User, UserProfileResponse>();
            CreateMap<User, UserResponse>()
                .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.Role != null ? src.Role.Name.ToString() : null));
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

            //==================ASSEMBLED PRODUCT=======================//
            CreateMap<AssembledProduct, AssembledProductResponse>();
            CreateMap<AssembledProduct, AssembledProductDetailResponse>()
                .ForMember(dest => dest.Details, opt => opt.MapFrom(src => src.ProductAssembledDetails));

            CreateMap<ProductAssembledDetail, ProductAssembledDetailResponse>()
                .ForMember(dest => dest.BaseKitName, opt => opt.MapFrom(src => src.BaseKit != null ? src.BaseKit.Name : string.Empty))
                .ForMember(dest => dest.ComponentName, opt => opt.MapFrom(src => src.Component != null ? src.Component.Name : string.Empty));

            CreateMap<CreateAssembledProductRequest, AssembledProduct>()
                .ForMember(dest => dest.ProductAssembledDetails, opt => opt.MapFrom(src => src.Details));

            CreateMap<ProductAssembledDetailRequest, ProductAssembledDetail>();

            CreateMap<UpdateAssembledProductRequest, AssembledProduct>();
            //==================ASSEMBLED PRODUCT=======================//

            //==================MODEL=======================//
            CreateMap<Model, PartResponse>()
                .ForMember(dest => dest.ShopName, opt => opt.MapFrom(src => src.Shop.ShopName))
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name))
                .ForMember(dest => dest.RecipeSwitchCount, opt => opt.MapFrom(src =>
                    GetRecipeValue(src.Specifications, "switch")))
                .ForMember(dest => dest.RecipeStabilizerCount, opt => opt.MapFrom(src =>
                    GetRecipeValue(src.Specifications, "stabilizer")))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src =>
                    src.StockQuantity > 0 ? StockStatus.InStock : StockStatus.OutOfStock));

            CreateMap<CreateUpdatePartRequest, Model>()
                .ForMember(dest => dest.ThumbnailURL, opt => opt.Ignore())
                .ForMember(dest => dest.DefaultLayerImageUrl, opt => opt.Ignore())
                .ForMember(dest => dest.Slug, opt => opt.Ignore());

            //==================MODEL=======================//

            //==================KITDESIGN=======================//

            CreateMap<KitDesignOption, CompatiblePartResponse>()
                .ForMember(dest => dest.PartId, opt => opt.MapFrom(src => src.ComponentId))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Component.Name))
                .ForMember(dest => dest.Price, opt => opt.MapFrom(src => src.Component.Price))
                .ForMember(dest => dest.ThumbnailUrl, opt => opt.MapFrom(src => src.Component.ThumbnailURL))
                .ForMember(dest => dest.IsDefault, opt => opt.MapFrom(src => src.IsDefault))
                .ForMember(dest => dest.LayerImageUrl, opt => opt.MapFrom(src =>
                    !string.IsNullOrEmpty(src.LayerImageUrl)
                        ? src.LayerImageUrl
                        : src.Component.DefaultLayerImageUrl))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src =>
                    src.Component != null && src.Component.StockQuantity > 0 ? StockStatus.InStock : StockStatus.OutOfStock));

            CreateMap<CreateKitOptionRequest, KitDesignOption>()
                .ForMember(dest => dest.LayerImageUrl, opt => opt.Ignore()); // Handled in service
            //==================KITDESIGN=======================//

           
            //==================ShopProfile=====================//
            CreateMap<ShopProfile, ShopResponse>();

            CreateMap<ShopProfile, ShopDetailResponse>();

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
            CreateMap<OrderGroup, OrderGroupResponse>()
                .ForMember(dest => dest.PaymentStatus, opt => opt.MapFrom(src => src.PaymentStatus));

            // =========================================================
            // 2. ORDER (ORDER -> DTO)
            // =========================================================
            CreateMap<Order, OrderResponse>()
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
            CreateMap<OrderItem, OrderItemResponse>()
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

               .ForMember(dest => dest.IsCustom, opt => opt.MapFrom(src => src.IsCustom))
               .ForMember(dest => dest.OrderItemComponents, opt => opt.MapFrom(src => src.OrderItemComponents));
            //order item component
            CreateMap<OrderItemComponent, OrderItemComponentDto>();

        }


        //HELPER
        private int GetRecipeValue(string? jsonSpecs, string key)
        {
            if (string.IsNullOrEmpty(jsonSpecs)) return 0;
            try
            {
                using (var doc = JsonDocument.Parse(jsonSpecs))
                {
                    if (doc.RootElement.TryGetProperty("recipe", out var recipe))
                    {
                        // Tìm property có tên chứa key (ví dụ "switch" trong "switch")
                        // Loop qua các property để tìm flexible (vd case sensitive)
                        foreach (var prop in recipe.EnumerateObject())
                        {
                            if (prop.Name.ToLower().Contains(key.ToLower()) && prop.Value.ValueKind == JsonValueKind.Number)
                            {
                                return prop.Value.GetInt32();
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore parsing errors
            }
            return 0;
        }

    }
    
}
