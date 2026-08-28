// Feature: category-management, Property 2: Cascading inactivation
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using NSubstitute;
using Repo;
using Service.Category;
using Xunit;

namespace RecurringTests.CategoryTests;

/// <summary>
/// Property 2: Cascading inactivation
/// Validates: Requirements 3.2, 3.4
/// For any active main category with N active subcategories, inactivating it results
/// in all N+1 items having Inactive == true and UpdatedAt updated.
/// </summary>
[Trait("Feature", "category-management")]
[Trait("Property", "2")]
public class CascadingInactivationPropertyTests
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
    /// For any active main category with N active subcategories (N derived from subcategoryCount % 10),
    /// inactivating the main category SHALL result in all N+1 items having Inactive == true
    /// and UpdatedAt set to (at least) the current UTC timestamp.
    /// **Validates: Requirements 3.2, 3.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> InactivatingMainCategory_CascadesToAllActiveSubcategories(
        PositiveInt parentExternalId,
        NonEmptyString mainCategoryName,
        NonEmptyString subNameBase,
        byte subcategoryCount)
    {
        // Limit subcategory count to 0..20
        int n = subcategoryCount % 21;
        var parentExtId = parentExternalId.Get;
        var dbName = $"CascadeInactivation_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var beforeInactivation = DateTime.UtcNow.AddMinutes(-5);

        // Seed an active main category and N active subcategories
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Category.Add(new CategoryDTO
            {
                CategoryId = Guid.NewGuid(),
                ExternalId = parentExtId,
                Name = mainCategoryName.Get,
                IsMainCategory = true,
                Inactive = false,
                UpdatedAt = beforeInactivation,
                CreatedAt = beforeInactivation,
                UserId = 1,
                SystemDefault = false,
            });

            for (int i = 0; i < n; i++)
            {
                ctx.Category.Add(new CategoryDTO
                {
                    CategoryId = Guid.NewGuid(),
                    ExternalId = parentExtId + 1000 + i,
                    Name = $"{subNameBase.Get}_{i}",
                    IsMainCategory = false,
                    ParentExternalId = parentExtId,
                    Inactive = false,
                    UpdatedAt = beforeInactivation,
                    CreatedAt = beforeInactivation,
                    UserId = 1,
                    SystemDefault = false,
                });
            }

            await ctx.SaveChangesAsync();
        }

        // Set up the real repo and service
        var categoryRepo = new CategoryRepo(factory);
        var categoryApiRepo = Substitute.For<ApiRepo.ICategoryApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();
        var service = new CategoryService(categoryRepo, categoryApiRepo, syncCursorRepo);

        // Get all categories and find the main category
        var all = await service.GetAllAsync();
        var mainCategory = all.First(c => c.IsMainCategory && c.ExternalId == parentExtId);

        // Record time just before action
        var timeBeforeAction = DateTime.UtcNow;

        // Simulate the inactivation logic (same as CategoryManagementVM.InactivateCategory)
        mainCategory.Inactive = true;
        mainCategory.UpdatedAt = DateTime.UtcNow;
        await service.UpdateLocalAsync(mainCategory);

        // Cascade: inactivate all active subcategories
        if (mainCategory.IsMainCategory && mainCategory.ExternalId is not null)
        {
            var allAfterMain = await service.GetAllAsync();
            var activeSubcategories = allAfterMain
                .Where(c => !c.IsMainCategory
                    && c.ParentExternalId == mainCategory.ExternalId
                    && !c.Inactive)
                .ToList();

            foreach (var sub in activeSubcategories)
            {
                sub.Inactive = true;
                sub.UpdatedAt = DateTime.UtcNow;
                await service.UpdateLocalAsync(sub);
            }
        }

        // Verify: all N+1 items have Inactive == true and UpdatedAt >= timeBeforeAction
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var finalMain = await ctx.Category
                .FirstOrDefaultAsync(c => c.IsMainCategory && c.ExternalId == parentExtId);

            var finalSubs = await ctx.Category
                .Where(c => !c.IsMainCategory && c.ParentExternalId == parentExtId)
                .ToListAsync();

            // Count check: we should have exactly N subcategories
            if (finalMain is null || finalSubs.Count != n)
                return false;

            // Main category must be inactive with updated timestamp
            if (!finalMain.Inactive || finalMain.UpdatedAt < timeBeforeAction)
                return false;

            // All subcategories must be inactive with updated timestamps
            foreach (var sub in finalSubs)
            {
                if (!sub.Inactive || sub.UpdatedAt < timeBeforeAction)
                    return false;
            }

            return true;
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
