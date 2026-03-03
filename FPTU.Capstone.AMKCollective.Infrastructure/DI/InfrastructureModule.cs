using Autofac;
using FPTU.Capstone.AMKCollective.Application.DTOs.Settings;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Infrastructure.Configurations;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using FPTU.Capstone.AMKCollective.Infrastructure.Services;
using FPTU.Capstone.AMKCollective.Infrastructure.ThirdParty;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FPTU.Capstone.AMKCollective.Infrastructure.DI
{
    public class InfrastructureModule : Module
    {
        private readonly IConfiguration _configuration;

        public InfrastructureModule(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<UnitOfWork>().As<IUnitOfWork>().InstancePerLifetimeScope();
            // Register DbContext
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

            builder.Register(c =>
            {
                var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                optionsBuilder.UseMySql(connectionString, serverVersion, mySqlOptions =>
                    mySqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null));
                return new ApplicationDbContext(optionsBuilder.Options);
            })
            .AsSelf()
            .InstancePerLifetimeScope();
            builder.Register(c =>
            {
                var settings = new StripeSettings();
                _configuration.GetSection("StripeSettings").Bind(settings);
                return Options.Create(settings);
            })
            .As<IOptions<StripeSettings>>()
            .SingleInstance();


            // Register repositories and services used by the application
            builder.RegisterType<ThirdPartyClient>().As<IThirdPartyClient>().SingleInstance();
            builder.RegisterType<EmailService>().As<IEmailService>().InstancePerLifetimeScope();

            builder.RegisterType<PasswordHasher<User>>()
           .As<IPasswordHasher<User>>()
           .InstancePerLifetimeScope();

            builder.RegisterAssemblyTypes(typeof(InfrastructureModule).Assembly)
               .Where(t => t.Name.EndsWith("Repository"))
               .AsImplementedInterfaces()
               .InstancePerLifetimeScope();
            builder.RegisterAssemblyTypes(typeof(StripePaymentService).Assembly)
       .Where(t => t.Name.EndsWith("Service"))
       .AsImplementedInterfaces()
       .InstancePerLifetimeScope();
            // Register application services (concrete implementation type is in App project)
            // builder.RegisterAssemblyTypes(typeof(FPTU.Capstone.AMKCollective.Application.Services.UserService).Assembly)
            //     .Where(t => t.Name.EndsWith("Service"))
            //     .AsImplementedInterfaces()
            //     .InstancePerLifetimeScope();

            RegisterOptions<OrderSettings>(builder, "OrderSettings");
            RegisterOptions<FrontendUrls>(builder, "FrontendUrls");
            RegisterOptions<BuilderSettings>(builder, "BuilderSettings");
            RegisterOptions<VoucherSettings>(builder, "VoucherSettings");
            RegisterOptions<WalletSettings>(builder, "WalletSettings");
            RegisterOptions<WorkerIntervals>(builder, "WorkerIntervals");
        }

        private void RegisterOptions<T>(ContainerBuilder builder, string sectionName) where T : class, new()
        {
            builder.Register(c =>
            {
                var settings = new T();
                _configuration.GetSection(sectionName).Bind(settings);
                return Options.Create(settings); 
            })
            .As<IOptions<T>>()
            .SingleInstance();
        }
    }
}
