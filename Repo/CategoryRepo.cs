using Microsoft.EntityFrameworkCore;
using Model.DTO;
using System;
using System.Collections.Generic;
using System.Text;

namespace Repo
{
    public interface ICategoryRepo
    {
        Task AddAsync(CategoryDTO category);
        Task<List<CategoryDTO>> GetAllAsync();
    }

    public class CategoryRepo(IDbContextFactory<DbCtx> DbCtx) : ICategoryRepo
    {
        public async Task<List<Model.DTO.CategoryDTO>> GetAllAsync()
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Category.ToListAsync();
        }

        public async Task AddAsync(Model.DTO.CategoryDTO category)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            db.Category.Add(category);
            await db.SaveChangesAsync();
        }
    }
}
