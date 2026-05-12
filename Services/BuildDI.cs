using ApiRepo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Repo;

namespace Service
{
    public static class BuildDI
    {
        public static void AddDbContext(this IServiceCollection services)
        {
            services.AddDbContextFactory<DbCtx>(options => options
            .UseSqlite($"Filename={Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XpemFinancial.db")}")
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
        }

        public static void AddRepo(this IServiceCollection services)
        {
            services.AddScoped<IUserRepo, UserRepo>();
            services.AddScoped<IAccountRepo, AccountRepo>();
            services.AddScoped<ITransactionRepo, TransactionRepo>();
            services.AddScoped<ICategoryRepo, CategoryRepo>();
        }

        public static void AddService(this IServiceCollection services)
        {
            services.AddScoped<IBuildDbService, BuildDbService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IAccountService, AccountService>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<ITransactionService, TransactionService>();
            services.AddScoped<IUserSessionService, UserSessionService>();
        }

        public static void AddApiRepo(this IServiceCollection services)
        {
            services.AddScoped<IUserApiRepo, UserApiRepo>();
        }
        }
}
