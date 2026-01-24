using FPTU.Capstone.AMKCollective.Application.Interfaces;
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
            
            return services;
        }
    }
}
