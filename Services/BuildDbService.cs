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
        Task InitAsync();
        Task CleanLocalDatabaseAsync();
    }

    public class BuildDbService(IDbContextFactory<DbCtx> DbCtx) : IBuildDbService
    {
        private const int CurrentDbVersion = 15;

        public async Task InitAsync()
        {
            await using var context = await DbCtx.CreateDbContextAsync();

            await context.Database.EnsureCreatedAsync();

            var actualVersion = context.VersionDbTables.FirstOrDefault();

            if (actualVersion is null || actualVersion.Version != CurrentDbVersion)
            {
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();

                context.VersionDbTables.Add(new VersionDbTablesDTO { Id = 0, Version = CurrentDbVersion });
                await context.SaveChangesAsync();
            }
        }

        public async Task CleanLocalDatabaseAsync()
        {
            await using var context = await DbCtx.CreateDbContextAsync();

            // EnsureDeletedAsync tries to delete the .db file, which fails on Android/iOS
            // when EF's connection pool still holds an open handle to it.
            // Instead, wipe every table inside a single transaction — safe with live connections.
            await using var tx = await context.Database.BeginTransactionAsync();

            // Order matters: children before parents to satisfy FK constraints.
            await context.Database.ExecuteSqlRawAsync("DELETE FROM \"SyncCursor\"");
            await context.Database.ExecuteSqlRawAsync("DELETE FROM \"Transaction\"");
            await context.Database.ExecuteSqlRawAsync("DELETE FROM \"RecurringRule\"");
            await context.Database.ExecuteSqlRawAsync("DELETE FROM \"Account\"");
            await context.Database.ExecuteSqlRawAsync("DELETE FROM \"Category\"");
            await context.Database.ExecuteSqlRawAsync("DELETE FROM \"User\"");
            await context.Database.ExecuteSqlRawAsync("DELETE FROM \"VersionDbTables\"");

            await tx.CommitAsync();

            // Re-insert the version row so InitAsync does not drop/recreate on the next launch.
            context.VersionDbTables.Add(new VersionDbTablesDTO { Id = 0, Version = CurrentDbVersion });
            await context.SaveChangesAsync();
        }
    }
}
