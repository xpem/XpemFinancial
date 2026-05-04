using Microsoft.Extensions.DependencyInjection;
using Repo;

namespace Service.DI
{
    public static class BuildRepo
    {
        public static void AddRepo(this IServiceCollection services)
        {
            services.AddScoped<IUserRepo, UserRepo>();
            services.AddScoped<IAccountRepo, AccountRepo>();
            services.AddScoped<ITransactionRepo, TransactionRepo>();
            services.AddScoped<ICategoryRepo, CategoryRepo>();
        }
    }
}
