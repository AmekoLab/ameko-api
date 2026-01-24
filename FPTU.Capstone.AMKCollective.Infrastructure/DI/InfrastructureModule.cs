using Autofac;
using FPTU.Capstone.AMKCollective.Application.Interfaces;
using FPTU.Capstone.AMKCollective.Infrastructure.Services;
using FPTU.Capstone.AMKCollective.Infrastructure.ThirdParty;
using FPTU.Capstone.AMKCollective.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
                optionsBuilder.UseMySql(connectionString, serverVersion);
                return new ApplicationDbContext(optionsBuilder.Options);
            })
            .AsSelf()
            .InstancePerLifetimeScope();



            // Register repositories and services used by the application
            builder.RegisterType<ThirdPartyClient>().As<IThirdPartyClient>().SingleInstance();
            builder.RegisterType<EmailService>().As<IEmailService>().InstancePerLifetimeScope();

            builder.RegisterAssemblyTypes(typeof(InfrastructureModule).Assembly)
               .Where(t => t.Name.EndsWith("Repository"))
               .AsImplementedInterfaces()
               .InstancePerLifetimeScope();

            builder.RegisterAssemblyTypes(typeof(InfrastructureModule).Assembly)
               .Where(t => t.Name.EndsWith("Service"))
               .AsImplementedInterfaces()
               .InstancePerLifetimeScope();

            // Register application services (concrete implementation type is in App project)
            // builder.RegisterAssemblyTypes(typeof(FPTU.Capstone.AMKCollective.Application.Services.UserService).Assembly)
            //     .Where(t => t.Name.EndsWith("Service"))
            //     .AsImplementedInterfaces()
            //     .InstancePerLifetimeScope();
        }
    }
}
