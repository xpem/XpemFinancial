using Microsoft.Extensions.DependencyInjection;
using Repo;
using System;
using System.Collections.Generic;
using System.Text;

namespace Service.DI
{
    public static class BuildRepo
    {
        public static void AddRepo(this IServiceCollection services)
        {
            services.AddScoped<IUserRepo, UserRepo>();
        }
    }
}
