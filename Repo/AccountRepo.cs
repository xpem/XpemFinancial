using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Repo
{
    public class AccountRepo(IDbContextFactory<DbCtx> DbCtx) : IAccountRepo
    {
        public async Task Add(Model.DTO.AccountDTO account)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            db.Account.Add(account);
            await db.SaveChangesAsync();
        }

        //por enquanto só teremos uma conta e um usuário
        public async Task<Model.DTO.AccountDTO?> GetAsync()
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Account.FirstOrDefaultAsync();
        }
    }
