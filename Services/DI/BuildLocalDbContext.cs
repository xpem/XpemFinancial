using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Repo;

namespace Service.DI
{
    public static class BuildLocalDbContext
    {
        public static void AddDbContext(this IServiceCollection services)
        {
            services.AddDbContextFactory<DbCtx>(options => options
            .UseSqlite($"Filename={Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XpemFinancial.db")}")
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
        }
    }
}
