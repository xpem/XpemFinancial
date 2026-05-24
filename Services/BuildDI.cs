using ApiRepo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Repo;
using Service.Account;
using Service.Category;
using Service.Transaction;

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

        public static void AddService(this IServiceCollection services)
        {
            // core
            services.AddTransient<IBuildDbService, BuildDbService>();
            services.AddTransient<IUserSessionService, UserSessionService>();

            // user
            services.AddTransient<IUserService, UserService>();

            // account
            services.AddTransient<IAccountService, AccountService>();

            // transaction
            services.AddTransient<ITransactionService, TransactionService>();

            // category
            services.AddTransient<ICategoryService, CategoryService>();
        }

        public static void AddRepo(this IServiceCollection services)
        {
            services.AddTransient<IUserRepo, UserRepo>();
            services.AddTransient<IAccountRepo, AccountRepo>();
            services.AddTransient<ITransactionRepo, TransactionRepo>();
            services.AddTransient<ICategoryRepo, CategoryRepo>();
        }

        public static void AddApiRepo(this IServiceCollection services)
        {
            services.AddTransient<IUserApiRepo, UserApiRepo>();
            services.AddTransient<ICategoryApiRepo, CategoryApiRepo>();
            services.AddTransient<IAccountApiRepo, AccountApiRepo>();
            services.AddTransient<ITransactionApiRepo, TransactionApiRepo>();
        }
    }
}
