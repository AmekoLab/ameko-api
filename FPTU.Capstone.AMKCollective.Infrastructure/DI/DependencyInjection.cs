using FPTU.Capstone.AMKCollective.Application.Interfaces.AI;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Infrastructure.Services;
using FPTU.Capstone.AMKCollective.Infrastructure.ThirdParty.AI;
using Microsoft.Extensions.DependencyInjection;

namespace FPTU.Capstone.AMKCollective.Infrastructure.DI
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
        {
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IWithdrawalRequestRepository, WithdrawalRequestRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<ITokenService, TokenService>();
            
            // AI Services
            services.AddScoped<IEmbeddingService, GoogleEmbeddingService>();
            services.AddScoped<IQdrantService, QdrantService>();
            services.AddHttpClient<IWebSearchService, TavilySearchService>();

            return services;
        }
    }
}
