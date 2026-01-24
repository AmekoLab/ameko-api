using FPTU.Capstone.AMKCollective.Application.Interfaces;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Application.Services;
using FPTU.Capstone.AMKCollective.Infrastructure.Services;

namespace FPTU.Capstone.AMKCollective.API.Extensions
{
    public static class ServiceExtensions
    {
        public static void AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<IModelRepository, ModelRepository>();
            services.AddScoped<IKitDesignOptionRepository, KitDesignOptionRepository>();


            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<ICustomBuilderService, CustomBuilderService>();
            services.AddScoped<IProductService, ProductService>();



        }
    }
}
