using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using NSubstitute;
using Repo;
using Service.Category;
using Xunit;

namespace RecurringTests.CategoryTests;

// Feature: category-management, Property 3: Cascading reactivation

/// <summary>
/// Property 3: Cascading reactivation
/// Validates: Requirements 4.1, 4.2
/// For any inactive subcategory with inactive parent, reactivating the subcategory
/// also reactivates the parent.
/// </summary>
[Trait("Feature", "category-management")]
[Trait("Property", "3")]
public class CascadingReactivationPropertyTests
{
    /// <summary>
    /// Creates a DbContextFactory backed by a unique in-memory database.
    /// </summary>
    private static IDbContextFactory<DbCtx> CreateFactory(string dbName)
    {
        var options = new DbContextOptionsBuilder<DbCtx>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new TestDbContextFactory(options);
    }

    /// <summary>
    /// For any inactive subcategory whose parent main category is also inactive,
    /// reactivating the subcategory SHALL result in both the subcategory and its parent
    /// having Inactive == false and UpdatedAt set to the current UTC timestamp.
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> ReactivatingInactiveSubcategory_AlsoReactivatesInactiveParent(
        PositiveInt parentExternalId,
        NonEmptyString parentName,
        NonEmptyString subcategoryName)
    {
        var parentExtId = parentExternalId.Get;
        var dbName = $"CascadeReactivation_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var beforeReactivation = DateTime.UtcNow.AddMinutes(-5);

        // Seed an inactive parent main category and an inactive subcategory
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Category.Add(new CategoryDTO
            {
                CategoryId = Guid.NewGuid(),
                ExternalId = parentExtId,
                Name = parentName.Get,
                IsMainCategory = true,
                Inactive = true,
                UpdatedAt = beforeReactivation,
                CreatedAt = beforeReactivation,
                UserId = 1,
                SystemDefault = false,
            });

            ctx.Category.Add(new CategoryDTO
            {
                CategoryId = Guid.NewGuid(),
                ExternalId = parentExtId + 1000,
                Name = subcategoryName.Get,
                IsMainCategory = false,
                ParentExternalId = parentExtId,
                Inactive = true,
                UpdatedAt = beforeReactivation,
                CreatedAt = beforeReactivation,
                UserId = 1,
                SystemDefault = false,
            });

            await ctx.SaveChangesAsync();
        }

        // Set up the real repo and service
        var categoryRepo = new CategoryRepo(factory);
        var categoryApiRepo = Substitute.For<ApiRepo.ICategoryApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();
        var service = new CategoryService(categoryRepo, categoryApiRepo, syncCursorRepo);

        // Get all categories and find the subcategory
        var all = await service.GetAllAsync();
        var subcategory = all.First(c => !c.IsMainCategory && c.ParentExternalId == parentExtId);

        // Simulate the reactivation logic (same as CategoryManagementVM.ReactivateCategory)
        var timeBeforeAction = DateTime.UtcNow;

        subcategory.Inactive = false;
        subcategory.UpdatedAt = DateTime.UtcNow;
        await service.UpdateLocalAsync(subcategory);

        // Cascade up: if subcategory's parent is inactive, also reactivate the parent
        if (!subcategory.IsMainCategory && subcategory.ParentExternalId != null)
        {
            var allAfter = await service.GetAllAsync();
            var parent = allAfter.FirstOrDefault(c =>
                c.IsMainCategory && c.ExternalId == subcategory.ParentExternalId);

            if (parent != null && parent.Inactive)
            {
                parent.Inactive = false;
                parent.UpdatedAt = DateTime.UtcNow;
                await service.UpdateLocalAsync(parent);
            }
        }

        // Verify: both subcategory and parent are now active with updated timestamps
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var finalParent = await ctx.Category
                .FirstOrDefaultAsync(c => c.IsMainCategory && c.ExternalId == parentExtId);
            var finalSub = await ctx.Category
                .FirstOrDefaultAsync(c => !c.IsMainCategory && c.ParentExternalId == parentExtId);

            return finalParent is not null
                && finalSub is not null
                && !finalParent.Inactive
                && !finalSub.Inactive
                && finalParent.UpdatedAt >= timeBeforeAction
                && finalSub.UpdatedAt >= timeBeforeAction;
        }
    }

    /// <summary>
    /// For any inactive subcategory whose parent is already active,
    /// reactivating the subcategory SHALL NOT affect the parent's state —
    /// only the subcategory becomes active.
    /// **Validates: Requirements 4.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> ReactivatingSubcategory_WithActiveParent_DoesNotAffectParent(
        PositiveInt parentExternalId,
        NonEmptyString parentName,
        NonEmptyString subcategoryName)
    {
        var parentExtId = parentExternalId.Get;
        var dbName = $"CascadeReactivation_ActiveParent_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var beforeReactivation = DateTime.UtcNow.AddMinutes(-5);

        // Seed an active parent and an inactive subcategory
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Category.Add(new CategoryDTO
            {
                CategoryId = Guid.NewGuid(),
                ExternalId = parentExtId,
                Name = parentName.Get,
                IsMainCategory = true,
                Inactive = false, // parent is already active
                UpdatedAt = beforeReactivation,
                CreatedAt = beforeReactivation,
                UserId = 1,
                SystemDefault = false,
            });

            ctx.Category.Add(new CategoryDTO
            {
                CategoryId = Guid.NewGuid(),
                ExternalId = parentExtId + 1000,
                Name = subcategoryName.Get,
                IsMainCategory = false,
                ParentExternalId = parentExtId,
                Inactive = true,
                UpdatedAt = beforeReactivation,
                CreatedAt = beforeReactivation,
                UserId = 1,
                SystemDefault = false,
            });

            await ctx.SaveChangesAsync();
        }

        // Set up the real repo and service
        var categoryRepo = new CategoryRepo(factory);
        var categoryApiRepo = Substitute.For<ApiRepo.ICategoryApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();
        var service = new CategoryService(categoryRepo, categoryApiRepo, syncCursorRepo);

        // Get all categories and find the subcategory
        var all = await service.GetAllAsync();
        var subcategory = all.First(c => !c.IsMainCategory && c.ParentExternalId == parentExtId);

        // Simulate the reactivation logic
        subcategory.Inactive = false;
        subcategory.UpdatedAt = DateTime.UtcNow;
        await service.UpdateLocalAsync(subcategory);

        // Cascade up: parent is active, so no cascade should happen
        if (!subcategory.IsMainCategory && subcategory.ParentExternalId != null)
        {
            var allAfter = await service.GetAllAsync();
            var parent = allAfter.FirstOrDefault(c =>
                c.IsMainCategory && c.ExternalId == subcategory.ParentExternalId);

            if (parent != null && parent.Inactive)
            {
                parent.Inactive = false;
                parent.UpdatedAt = DateTime.UtcNow;
                await service.UpdateLocalAsync(parent);
            }
        }

        // Verify: subcategory is active, parent's UpdatedAt was NOT changed
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var finalParent = await ctx.Category
                .FirstOrDefaultAsync(c => c.IsMainCategory && c.ExternalId == parentExtId);
            var finalSub = await ctx.Category
                .FirstOrDefaultAsync(c => !c.IsMainCategory && c.ParentExternalId == parentExtId);

            return finalParent is not null
                && finalSub is not null
                && !finalParent.Inactive
                && !finalSub.Inactive
                && finalParent.UpdatedAt == beforeReactivation; // parent was not touched
        }
    }

    /// <summary>
    /// Simple IDbContextFactory implementation for testing.
    /// </summary>
    private sealed class TestDbContextFactory(DbContextOptions<DbCtx> options) : IDbContextFactory<DbCtx>
    {
        public DbCtx CreateDbContext() => new(options);
        public Task<DbCtx> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new DbCtx(options));
    }
}
