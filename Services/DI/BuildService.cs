using Microsoft.Extensions.DependencyInjection;

namespace Service.DI
{
    public static class BuildService
    {
        public static void AddService(this IServiceCollection services)
        {
            services.AddScoped<IBuildDbService, BuildDbService>();
            services.AddScoped<IUserService, UserService>();
        }
    }
}
