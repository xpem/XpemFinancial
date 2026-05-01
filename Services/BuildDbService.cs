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
        private const int CurrentDbVersion = 1;

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

            // Recria o banco do zero em vez de remover tabela por tabela
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }
    }
}
