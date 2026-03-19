using Autofac;

namespace FPTU.Capstone.AMKCollective.Application.DI
{
    public class ApplicationModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // Register all application services by convention
            builder.RegisterAssemblyTypes(typeof(FPTU.Capstone.AMKCollective.Application.Services.UserService).Assembly)
                .Where(t => t.Name.EndsWith("Service"))
                .AsImplementedInterfaces()
                .InstancePerLifetimeScope();

            // Register Enrichers
            builder.RegisterType<FPTU.Capstone.AMKCollective.Application.Services.ProductPostEnricher>()
                .As<FPTU.Capstone.AMKCollective.Application.Interfaces.Services.IPostEnricher>()
                .InstancePerLifetimeScope();
        }
    }
}
