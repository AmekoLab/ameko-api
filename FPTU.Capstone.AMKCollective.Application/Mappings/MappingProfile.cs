using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs;
using FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProduct;
using FPTU.Capstone.AMKCollective.Application.DTOs.AssemblyTracking;
using FPTU.Capstone.AMKCollective.Application.DTOs.Auth;
using FPTU.Capstone.AMKCollective.Application.DTOs.Chat;
using FPTU.Capstone.AMKCollective.Application.DTOs.Commission;
using FPTU.Capstone.AMKCollective.Application.DTOs.Feedback;
using FPTU.Capstone.AMKCollective.Application.DTOs.Follow;
using FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues;
using FPTU.Capstone.AMKCollective.Application.DTOs.User;
using FPTU.Capstone.AMKCollective.Application.DTOs.Voucher;
using FPTU.Capstone.AMKCollective.Application.DTOs.Wallet;
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

            //==================CHAT=======================//
            CreateMap<Message, ChatMessageResponse>()
                // ConversationId comes from query context, not Message entity.
                .ForMember(dest => dest.ConversationId, opt => opt.Ignore());
            //==================CHAT=======================//

            //==================FOLLOW=======================//
            CreateMap<FollowRequest, Follow>();
            CreateMap<Follow, FollowResponse>();
            CreateMap<Follow, FollowedUserResponse>()
                .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Followed.Id))
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.Followed.Username))
                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.Followed.FirstName + " " + src.Followed.LastName))
                .ForMember(dest => dest.AvatarUrl, opt => opt.MapFrom(src => src.Followed.Image));

            CreateMap<Follow, FollowerResponse>()
                .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Follower.Id))
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.Follower.Username))
                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.Follower.FirstName + " " + src.Follower.LastName))
                .ForMember(dest => dest.AvatarUrl, opt => opt.MapFrom(src => src.Follower.Image));

            //==================ASSEMBLED PRODUCT=======================//
            CreateMap<AssembledProduct, AssembledProductResponse>()
                .ForMember(dest => dest.ShopId, opt => opt.MapFrom(src =>
                    src.ProductAssembledDetails.FirstOrDefault() != null && src.ProductAssembledDetails.First().BaseKit != null
                    ? src.ProductAssembledDetails.First().BaseKit.ShopId : Guid.Empty))
                .ForMember(dest => dest.ShopName, opt => opt.MapFrom(src =>
                    src.ProductAssembledDetails.FirstOrDefault() != null &&
                    src.ProductAssembledDetails.First().BaseKit != null &&
                    src.ProductAssembledDetails.First().BaseKit.Shop != null
                    ? src.ProductAssembledDetails.First().BaseKit.Shop.ShopName : string.Empty))
                .ForMember(dest => dest.LogoUrl, opt => opt.MapFrom(src =>
                    src.ProductAssembledDetails.FirstOrDefault() != null &&
                    src.ProductAssembledDetails.First().BaseKit != null &&
                    src.ProductAssembledDetails.First().BaseKit.Shop != null
                    ? src.ProductAssembledDetails.First().BaseKit.Shop.LogoUrl : null));

            CreateMap<AssembledProduct, AssembledProductDetailResponse>()
                .IncludeBase<AssembledProduct, AssembledProductResponse>()
                .ForMember(dest => dest.Details, opt => opt.MapFrom(src => src.ProductAssembledDetails));

            CreateMap<ProductAssembledDetail, ProductAssembledDetailResponse>()
                .ForMember(dest => dest.BaseKitName, opt => opt.MapFrom(src => src.BaseKit != null ? src.BaseKit.Name : string.Empty))
                .ForMember(dest => dest.ComponentName, opt => opt.MapFrom(src => src.Component != null ? src.Component.Name : string.Empty));

            CreateMap<CreateAssembledProductRequest, AssembledProduct>()
                .ForMember(dest => dest.ProductAssembledDetails, opt => opt.MapFrom(src => src.Details));

            CreateMap<ProductAssembledDetailRequest, ProductAssembledDetail>();

            CreateMap<UpdateAssembledProductRequest, AssembledProduct>()
                //Tuan Note: Do not map Details here to avoid overwriting existing details, please do not remove this line ^^
                .ForMember(dest => dest.ProductAssembledDetails, opt => opt.Ignore());
            //==================ASSEMBLED PRODUCT=======================//

            //==================MODEL=======================//
            CreateMap<Model, PartResponse>()
                .ForMember(dest => dest.ShopName, opt => opt.MapFrom(src => src.Shop.ShopName))
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name))
                // [FIX] Explicitly map Specifications to ensure it's not skipped
                .ForMember(dest => dest.Specifications, opt => opt.MapFrom(src => src.Specifications))
                .ForMember(dest => dest.RecipeSwitchCount, opt => opt.MapFrom(src =>
                    GetRecipeValue(src.Specifications, "switch")))
                .ForMember(dest => dest.RecipeStabilizerCount, opt => opt.MapFrom(src =>
                    GetRecipeValue(src.Specifications, "stabilizer")))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src =>
                    src.StockQuantity > 0 ? StockStatus.InStock : StockStatus.OutOfStock));

            CreateMap<CreateUpdatePartRequest, Model>()
                .ForMember(dest => dest.ThumbnailURL, opt => opt.Ignore())
                .ForMember(dest => dest.DefaultLayerImageUrl, opt => opt.Ignore())
                .ForMember(dest => dest.Slug, opt => opt.Ignore())
                // [FIX] Explicitly map Specifications
                .ForMember(dest => dest.Specifications, opt => opt.MapFrom(src => src.Specifications));

            //==================MODEL=======================//

            //==================KITDESIGN=======================//

            CreateMap<KitDesignOption, CompatiblePartResponse>()
                .ForMember(dest => dest.OptionId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.PartId, opt => opt.MapFrom(src => src.ComponentId))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Component.Name))
                .ForMember(dest => dest.Price, opt => opt.MapFrom(src => src.Component.Price))
                .ForMember(dest => dest.ThumbnailUrl, opt => opt.MapFrom(src => src.Component.ThumbnailURL))
                .ForMember(dest => dest.IsDefault, opt => opt.MapFrom(src => src.IsDefault))
                .ForMember(dest => dest.LayerImageUrl, opt => opt.MapFrom(src =>
                    !string.IsNullOrEmpty(src.LayerImageUrl)
                        ? src.LayerImageUrl
                        : src.Component.DefaultLayerImageUrl))
                .ForMember(dest => dest.Tags, opt => opt.MapFrom(src => src.Tags))
                .ForMember(dest => dest.NextStepFilterRule, opt => opt.MapFrom(src => src.NextStepFilterRule))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src =>
                    src.Component != null && src.Component.StockQuantity > 0 ? StockStatus.InStock : StockStatus.OutOfStock));

            CreateMap<CreateKitOptionRequest, KitDesignOption>()
                .ForMember(dest => dest.LayerImageUrl, opt => opt.Ignore()); // Handled in service
            // Tags và NextStepFilterRule được AutoMapper map tự động (trùng tên)
            //==================KITDESIGN=======================//

           
            //==================ShopProfile=====================//
            CreateMap<ShopProfile, ShopResponse>();

            CreateMap<ShopProfile, ShopDetailResponse>()
                .ForMember(dest => dest.RemainingResubmits, opt => opt.MapFrom(src =>
                    CalculateRemainingResubmits(src.ResubmitCount, src.LastResubmitTime)));

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
                .ForMember(dest => dest.Status, opt => opt.Ignore())        // Status chỉ admin thay đổi
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())      // IsActive qua endpoint riêng
                .ForMember(dest => dest.ResubmitCount, opt => opt.Ignore()) // System managed
                .ForMember(dest => dest.LastResubmitTime, opt => opt.Ignore())
                .ForMember(dest => dest.Rating, opt => opt.Ignore())
                .ForMember(dest => dest.TotalSales, opt => opt.Ignore())
                .ForMember(dest => dest.TotalRevenue, opt => opt.Ignore())
                .ForMember(dest => dest.AdminNote, opt => opt.Ignore())
                .ForMember(dest => dest.CitizenId, opt => opt.Ignore())     // Không đổi sau khi đăng ký
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            CreateMap<ShopProfile, CurrentReputationDto>()
                .ForMember(dest => dest.ShopId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Badge, opt => opt.MapFrom(src => src.Badge.ToString()));
            CreateMap<QualityScoreSnapshot, QualityScoreSnapshotDto>()
                .ForMember(dest => dest.Badge, opt => opt.MapFrom(src => src.Badge.ToString()));
            CreateMap<QualityScoreSnapshot, ReputationTrendDto>()
                .ForMember(dest => dest.Date, opt => opt.MapFrom(src => src.CapturedAt.ToString("yyyy-MM-dd")))
                .ForMember(dest => dest.Score, opt => opt.MapFrom(src => src.TotalScore))
                .ForMember(dest => dest.Badge, opt => opt.MapFrom(src => src.Badge.ToString()));
            //==================ShopProfile=====================//


            // =========================================================
            // 1. ORDER GROUP (ORDER GROUP -> DTO)
            // =========================================================
            CreateMap<OrderGroup, OrderGroupResponse>()
                .ForMember(dest => dest.OrderGroupId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.PaymentStatus, opt => opt.MapFrom(src => src.PaymentStatus));

            // =========================================================
            // 2. ORDER (ORDER -> DTO)
            // =========================================================
            CreateMap<Order, OrderResponse>()
                .ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => src.Id))
        // 1. Xử lý Shop: BẮT BUỘC check null để không crash API GetCart
                .ForMember(dest => dest.ShopId, opt => opt.MapFrom(src => src.ShopId))
                .ForMember(dest => dest.ShopName, opt => opt.MapFrom(src => src.Shop != null ? src.Shop.ShopName : "N/A"))
    // Nếu Shop null thì Avatar cũng null
                .ForMember(dest => dest.ShopAvatar, opt => opt.MapFrom(src => src.Shop != null ? src.Shop.LogoUrl : null))

    // 2. Map Enum sang String: Để Frontend nhận được chữ "Pending", "Paid" thay vì số 0, 1
                .ForMember(dest => dest.OrderStatus, opt => opt.MapFrom(src => src.OrderStatus.ToString()))
                .ForMember(dest => dest.PaymentStatus, opt => opt.MapFrom(src => src.PaymentStatus.ToString()))

    // 3. Các field tiền nong (Giữ nguyên như cũ)
                .ForMember(dest => dest.SubTotal, opt => opt.MapFrom(src => src.SubTotal))
                .ForMember(dest => dest.ShippingFee, opt => opt.MapFrom(src => src.ShippingFee))
                .ForMember(dest => dest.DiscountAmount, opt => opt.MapFrom(src => src.DiscountAmount))
                .ForMember(dest => dest.SystemDiscountAmount, opt => opt.MapFrom(src => src.SystemDiscountAmount))
                .ForMember(dest => dest.TotalAmount, opt => opt.MapFrom(src => src.TotalAmount))

    // 4. Map OrderItems (Giữ nguyên)
                .ForMember(dest => dest.OrderItems, opt => opt.MapFrom(src => src.OrderItems))

                .ForMember(dest => dest.HasCancelRequest, opt => opt.MapFrom(src =>
                    src.OrderIssues != null && src.OrderIssues.Any(oi =>
                    oi.IsDeleted == false &&
                    oi.Type == OrderIssueType.CancelRequest &&
                    (oi.Status == OrderIssueStatus.Pending || oi.Status == OrderIssueStatus.InProgress)
                    )
                ))
                .ReverseMap();

            // =========================================================
            // 3. ORDER ITEM (ORDER ITEM -> DTO)
            // =========================================================
            CreateMap<OrderItem, OrderItemResponse>()               
                .ForMember(dest => dest.OrderItemId, opt => opt.MapFrom(src => src.Id))
               .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId))
               .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : src.ProductName))
               .ForMember(dest => dest.ProductImage, opt => opt.MapFrom(src => src.Product != null ? src.Product.ThumbnailURL : src.ProductImage))
               .ForMember(dest => dest.ShopId, opt => opt.MapFrom(src =>
                    src.Product != null ? src.Product.ShopId : GetShopIdFromConfig(src.DesignConfig)))

            .ForMember(dest => dest.ShopName, opt => opt.MapFrom(src =>
                src.Product != null && src.Product.Shop != null ? src.Product.Shop.ShopName : GetShopNameFromConfig(src.DesignConfig)))
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

            // =========================================================
            // ORDER ISSUES (ORDER ISSUES -> DTO)
            // =========================================================
            CreateMap<OrderIssue, OrderIssueResponse>()
                .ForMember(dest => dest.OrderTotalAmount, opt => opt.MapFrom(src => src.Order != null ? src.Order.TotalAmount : 0))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.User != null ? src.User.Username : string.Empty))
                .ForMember(dest => dest.ShopName, opt => opt.MapFrom(src =>
                    src.Order != null && src.Order.Shop != null ? src.Order.Shop.ShopName : string.Empty));

            CreateMap<OrderIssueLog, OrderIssueLogResponse>();
            // =========================================================
            // WALLET (WALLET -> DTO)
            // =========================================================

            // Map từ Wallet Entity -> WalletResponse DTO
            CreateMap<Wallet, WalletResponse>()
            .ForMember(dest => dest.HasPin, opt => opt.MapFrom(src => !string.IsNullOrEmpty(src.PinHash)));

            // Map từ Payment Entity -> WalletTransactionResponse DTO
            // Lưu ý: Cần convert Enum sang String cho Type và Status
            CreateMap<Payment, WalletTransactionResponse>()
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));

            // Map từ WithdrawRequest DTO -> Payment Entity (Dùng khi tạo lệnh rút tiền)
            // Lưu ý: Các field như FeeAmount, Status... sẽ được xử lý trong logic Service nên Ignore hoặc tự gán sau
            CreateMap<WithdrawRequest, Payment>()
                .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount));

            // =========================================================
            // VOUCHER (VOUCHER -> DTO)
            // =========================================================
            // Map Entity -> Response DTO
            CreateMap<Voucher, VoucherResponse>()
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
                .ForMember(dest => dest.DiscountType, opt => opt.MapFrom(src => src.DiscountType.ToString()))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.StackingPolicy, opt => opt.MapFrom(src => src.StackingPolicy.ToString()))
                .ForMember(dest => dest.Scope, opt => opt.MapFrom(src => src.Scope.ToString())) // Map Scope mới
                .ForMember(dest => dest.CreatorName, opt => opt.MapFrom(src =>
                    src.Creator != null && src.Creator.ShopProfile != null
                    ? src.Creator.ShopProfile.ShopName
                    : (src.Creator != null ? src.Creator.Username : "Unknown")));

            // Map Entity OrderVoucher -> DTO AppliedVoucherResponse
            CreateMap<VoucherUsageLog, AppliedVoucherResponse>()
                .ForMember(dest => dest.VoucherCode, opt => opt.MapFrom(src => src.Code)) 
                .ForMember(dest => dest.VoucherType, opt => opt.MapFrom(src => src.VoucherType.ToString()));

            // Map CreateRequest -> Entity
            CreateMap<CreateVoucherRequest, Voucher>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => VoucherStatus.Active))
                .ForMember(dest => dest.UsedCount, opt => opt.Ignore())
                .ForMember(dest => dest.CreatorId, opt => opt.Ignore());

            // Map UpdateRequest -> Entity
            CreateMap<UpdateVoucherRequest, Voucher>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));


            CreateMap<VoucherUsageLog, VoucherUsageResponse>()
                .ForMember(dest => dest.VoucherCode, opt => opt.MapFrom(src => src.Code)) // Lấy Code của Log
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Order.Customer.Username))
                .ForMember(dest => dest.OrderTotalAmount, opt => opt.MapFrom(src => src.Order.TotalAmount))
                .ForMember(dest => dest.AppliedAt, opt => opt.MapFrom(src => src.Order.CreatedAt));


            // =========================================================
            // PAYMENT (PAYMENT -> DTO)
            // =========================================================
            CreateMap<Payment, PaymentResponse>()
                // 1. Map Enum sang String 
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.Method, opt => opt.MapFrom(src => src.Method.ToString()))

                // 2. Map thông tin User (Flattening)
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src =>
                    src.User != null ? src.User.Username : "Unknown"))
                .ForMember(dest => dest.ShopName, opt => opt.MapFrom(src =>
                    src.User != null && src.User.ShopProfile != null ? src.User.ShopProfile.ShopName : null));


            CreateMap<AdjustBalanceRequest, Payment>()
                .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Reason))
                    // Các field khác như Type, Status sẽ gán trong Service
                .ForMember(dest => dest.UserId, opt => opt.Ignore());         

            // =========================================================
            // Commission (Commission -> DTO)
            // =========================================================
            CreateMap<CreateCommissionRequest, CommissionRequest>();
            CreateMap<SubmitQuoteRequest, CommissionQuote>();

            CreateMap<CommissionQuote, CommissionQuoteResponse>()
                .ForMember(dest => dest.CommissionQuoteId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.ShopName, opt => opt.MapFrom(src => src.Shop != null ? src.Shop.ShopName : ""))
                .ForMember(dest => dest.ShopAvatar, opt => opt.MapFrom(src => src.Shop != null ? src.Shop.LogoUrl : ""))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));

            CreateMap<CommissionRequest, CommissionRequestResponse>()
                .ForMember(dest => dest.CommissionRequestId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.UserName, opt => opt.MapFrom(src =>
                   src.User != null ? $"{src.User.FirstName} {src.User.LastName}".Trim() : ""))
                .ForMember(dest => dest.TargetedShopName, opt => opt.MapFrom(src =>         src.TargetedShop != null ? src.TargetedShop.ShopName : ""))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.Quotes, opt => opt.MapFrom(src => src.Quotes));

            //==================ASSEMBLY TRACKING MODULE=======================//
            // 1. Template Mapping
            CreateMap<AssemblyStepTemplate, AssemblyStepTemplateResponse>()
                .ForMember(dest => dest.TemplateId, opt => opt.MapFrom(src => src.Id));

            CreateMap<SaveAssemblyStepTemplateRequest, AssemblyStepTemplate>();

            // 2. Progress Log Mapping
            CreateMap<AssemblyProgressLog, AssemblyProgressLogResponse>()
                .ForMember(dest => dest.ProgressLogId, opt => opt.MapFrom(src => src.Id));

            //==================TRANSACTION=======================//
            CreateMap<Transaction, WalletTransactionResponse>()
                .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src =>
                    src.Type == TransactionType.SalesPending ? "Pending" : "Completed"))
                .ForMember(dest => dest.FeeAmount, opt => opt.MapFrom(src => 0m));

            CreateMap<Transaction, HeldTransactionResponse>()
                .ForMember(dest => dest.TransactionId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Date, opt => opt.MapFrom(src => src.CreatedAt))
                .ForMember(dest => dest.OrderId, opt => opt.MapFrom(src => src.RelatedOrderId))
                .ForMember(dest => dest.OrderStatus, opt => opt.MapFrom(src =>
                    src.RelatedOrder != null ? src.RelatedOrder.OrderStatus.ToString() : "Unknown"))
                .ForMember(dest => dest.Reason, opt => opt.MapFrom(src => "Reserved Funds (Until Order is Completed)"));

            // =========================================================
            // Feedback (Feedback -> DTO)
            // =========================================================
            CreateMap<Feedback, FeedbackResponse>()
                .ForMember(dest => dest.FeedbackId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.FromUserName, opt => opt.MapFrom(src => src.FromUser.Username))
                .ForMember(dest => dest.FromUserAvatar, opt => opt.MapFrom(src => src.FromUser.Image))
                .ForMember(dest => dest.CreatedDate, opt => opt.MapFrom(src => src.CreatedAt))
                .ForMember(dest => dest.ImageUrls, opt => opt.Ignore());

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

        private static int CalculateRemainingResubmits(int resubmitCount, DateTime? lastResubmitTime)
        {
            const int maxResubmitsPerMonth = 2;
            var now = DateTime.UtcNow;
            if (!lastResubmitTime.HasValue ||
                lastResubmitTime.Value.Year != now.Year ||
                lastResubmitTime.Value.Month != now.Month)
            {
                return maxResubmitsPerMonth;
            }

            return Math.Max(0, maxResubmitsPerMonth - resubmitCount);
        }

        private static Guid GetShopIdFromConfig(string? jsonConfig)
        {
            if (string.IsNullOrEmpty(jsonConfig)) return Guid.Empty;
            try
            {
                using var doc = JsonDocument.Parse(jsonConfig);
                if (doc.RootElement.TryGetProperty("ShopId", out var shopIdProp) && shopIdProp.TryGetGuid(out var shopId))
                    return shopId;
            }
            catch {  }
            return Guid.Empty;
        }

        private static string GetShopNameFromConfig(string? jsonConfig)
        {
            if (string.IsNullOrEmpty(jsonConfig)) return "N/A";
            try
            {
                using var doc = JsonDocument.Parse(jsonConfig);
                if (doc.RootElement.TryGetProperty("ShopName", out var shopNameProp))
                    return shopNameProp.GetString() ?? "N/A";
            }
            catch {  }
            return "N/A";
        }

    }
    
}
