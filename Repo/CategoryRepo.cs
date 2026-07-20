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
        Task<CategoryDTO?> GetByIdAsync(int id);
        Task<CategoryDTO?> GetByCategoryIdAsync(Guid categoryId);
        Task<bool> ExistsByNameAsync(string name, Guid? excludeCategoryId = null);
        Task UpdateAsync(Model.DTO.CategoryDTO category);
        Task<Model.DTO.CategoryDTO?> GetByExternalIdAsync(int externalId);
        Task<DateTime> GetMaxUpdatedAtAsync();
        Task<List<CategoryDTO>> GetPendingPushAsync();
    }

    public class CategoryRepo(IDbContextFactory<DbCtx> DbCtx) : ICategoryRepo
    {
        public async Task<List<Model.DTO.CategoryDTO>> GetAllAsync()
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Category.ToListAsync();
        }

        public async Task<CategoryDTO?> GetByIdAsync(int id)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Category.FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<CategoryDTO?> GetByCategoryIdAsync(Guid categoryId)
        {
            if (categoryId == Guid.Empty) return null;

            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Category.FirstOrDefaultAsync(c => c.CategoryId == categoryId);
        }

        public async Task<bool> ExistsByNameAsync(string name, Guid? excludeCategoryId = null)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Category.AnyAsync(c =>
                !c.Inactive &&
                c.Name.ToLower() == name.ToLower() &&
                (excludeCategoryId == null || c.CategoryId != excludeCategoryId));
        }

        public async Task AddAsync(Model.DTO.CategoryDTO category)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            db.Category.Add(category);
            await db.SaveChangesAsync();
        }

        public async Task UpdateAsync(Model.DTO.CategoryDTO category)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            db.Category.Update(category);
            await db.SaveChangesAsync();
        }

        public async Task<Model.DTO.CategoryDTO?> GetByExternalIdAsync(int externalId)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Category.FirstOrDefaultAsync(c => c.ExternalId == externalId);
        }

        public async Task<DateTime> GetMaxUpdatedAtAsync()
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Category.AnyAsync()
                ? await db.Category.MaxAsync(c => c.UpdatedAt)
                : DateTime.MinValue;
        }

        public async Task<List<CategoryDTO>> GetPendingPushAsync()
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Category
                .Where(c => c.CategoryId != Guid.Empty && c.ExternalId == null)
                .ToListAsync();
        }
    }
}
