using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Repo;
using System;
using System.Collections.Generic;
using System.Text;

namespace Service
{
    public interface IBuildDbService
    {
        Task CleanLocalDatabase();
        void Init();
    }

    public class BuildDbService(IDbContextFactory<DbCtx> DbCtx) : IBuildDbService
    {
        public void Init()
        {
            using var context = DbCtx.CreateDbContext();
            context.Database.EnsureCreated();

            VersionDbTablesDTO? actualVesionDbTables = context.VersionDbTables.FirstOrDefault();

            VersionDbTablesDTO newVersionDbTables = new() { Id = 0, Version = 6 };

            if (actualVesionDbTables != null)
            {
                if (actualVesionDbTables.Version != newVersionDbTables.Version)
                {
                    context.Database.EnsureDeleted();
                    context.Database.EnsureCreated();

                    actualVesionDbTables.Version = newVersionDbTables.Version;

                    context.VersionDbTables.Add(actualVesionDbTables);

                    context.SaveChanges();
                }
            }
            else
            {
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();

                context.VersionDbTables.Add(newVersionDbTables);

                context.SaveChanges();
            }
        }

        public async Task CleanLocalDatabase()
        {
            using var context = DbCtx.CreateDbContext();

            context.User.RemoveRange(context.User);
            context.Transaction.RemoveRange(context.Transaction);
            context.Category.RemoveRange(context.Category);

            await context.SaveChangesAsync();
        }
    }
}
