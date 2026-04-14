using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs.AssembledProduct;
using FPTU.Capstone.AMKCollective.Application.DTOs.AssemblyTracking;
using FPTU.Capstone.AMKCollective.Application.DTOs.Auth;
using FPTU.Capstone.AMKCollective.Application.DTOs.Commission;
using FPTU.Capstone.AMKCollective.Application.DTOs.Follow;
using FPTU.Capstone.AMKCollective.Application.DTOs.OrderIssues;
using FPTU.Capstone.AMKCollective.Application.DTOs.User;
using FPTU.Capstone.AMKCollective.Application.DTOs.Voucher;
using FPTU.Capstone.AMKCollective.Application.DTOs.Wallet;
using FPTU.Capstone.AMKCollective.Application.DTOs.Payment;
using FPTU.Capstone.AMKCollective.Application.DTOs.Part;
using FPTU.Capstone.AMKCollective.Application.DTOs.Builder;
using FPTU.Capstone.AMKCollective.Application.DTOs.Shop;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using FPTU.Capstone.AMKCollective.Application.DTOs.Withdrawal; // To cover WithdrawRequest

namespace FPTU.Capstone.AMKCollective.Application.Mappings
{
    /// <summary>
    /// Profile chuyên phục vụ request. Bảo toàn dữ liệu đầu vào chuần UTC.
    /// Không bao gồm ValueTransformer đổi múi giờ.
    /// </summary>
    public class RequestMappingProfile : Profile
    {
        public RequestMappingProfile()
        {
            CreateMap<RegisterRequest, User>()
                .ForMember(dest => dest.HashedPassword, opt => opt.Ignore())
                .ForMember(dest => dest.Role, opt => opt.Ignore());

            CreateMap<CreateUserRequest, User>()
                .ForMember(dest => dest.HashedPassword, opt => opt.Ignore())
                .ForMember(dest => dest.Role, opt => opt.Ignore());

            CreateMap<UpdateUserAdminRequest, User>()
                .ForMember(dest => dest.Role, opt => opt.Ignore());

            CreateMap<UpdateProfileRequest, User>();

            CreateMap<FollowRequest, Follow>();

            CreateMap<CreateAssembledProductRequest, AssembledProduct>()
                .ForMember(dest => dest.ProductAssembledDetails, opt => opt.MapFrom(src => src.Details));

            CreateMap<ProductAssembledDetailRequest, ProductAssembledDetail>();

            CreateMap<UpdateAssembledProductRequest, AssembledProduct>()
                //Tuan Note: Do not map Details here to avoid overwriting existing details, please do not remove this line ^^
                .ForMember(dest => dest.ProductAssembledDetails, opt => opt.Ignore());

            CreateMap<CreateUpdatePartRequest, Model>()
                .ForMember(dest => dest.ThumbnailURL, opt => opt.Ignore())
                .ForMember(dest => dest.DefaultLayerImageUrl, opt => opt.Ignore())
                .ForMember(dest => dest.Slug, opt => opt.Ignore())
                // [FIX] Explicitly map Specifications
                .ForMember(dest => dest.Specifications, opt => opt.MapFrom(src => src.Specifications));

            CreateMap<CreateKitOptionRequest, KitDesignOption>()
                .ForMember(dest => dest.LayerImageUrl, opt => opt.Ignore()); // Handled in service

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

            // Note: Withdrawal
            CreateMap<WithdrawRequest, Payment>()
                .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount));

            CreateMap<CreateVoucherRequest, Voucher>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => VoucherStatus.Active))
                .ForMember(dest => dest.UsedCount, opt => opt.Ignore())
                .ForMember(dest => dest.CreatorId, opt => opt.Ignore());

            CreateMap<UpdateVoucherRequest, Voucher>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            CreateMap<AdjustBalanceRequest, Payment>()
                .ForMember(dest => dest.Amount, opt => opt.MapFrom(src => src.Amount))
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Reason))
                .ForMember(dest => dest.UserId, opt => opt.Ignore());         

            CreateMap<CreateCommissionRequest, CommissionRequest>();
            CreateMap<SubmitQuoteRequest, CommissionQuote>();

            CreateMap<SaveAssemblyStepTemplateRequest, AssemblyStepTemplate>();
        }
    }
}
