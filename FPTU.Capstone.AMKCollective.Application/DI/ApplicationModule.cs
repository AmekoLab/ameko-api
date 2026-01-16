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
        }
    }
}
