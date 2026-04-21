using FPTU.Capstone.AMKCollective.Application.Interfaces.AI;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FPTU.Capstone.AMKCollective.Application.DI
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<ICustomBuilderService, CustomBuilderService>();
            services.AddScoped<IProductService, ProductService>();          
            services.AddScoped<IFollowService, FollowService>();
            services.AddScoped<IAssembledProductService, AssembledProductService>();
            services.AddScoped<IAssembledProductFeedbackService, AssembledProductFeedbackService>();
            services.AddScoped<IWarrantyService, WarrantyService>();
            services.AddScoped<IWithdrawalService, WithdrawalService>();
            services.AddScoped<IChatService, ChatService>();
            services.AddScoped<IAdminDashboardService, AdminDashboardService>();
            services.AddScoped<IShopDashboardService, ShopDashboardService>();
            services.AddScoped<IReputationService, ReputationService>();
            
            // AI Services
            services.AddHttpClient<IAIService, AIService>();


            services.AddSingleton<ISearchHistoryQueue, SearchHistoryQueue>();
            return services;
        }
    }
}
