// Feature: category-type-classification, Property 3: MainCategory type change cascades to active subcategories
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using NSubstitute;
using Repo;
using Service.Category;
using Xunit;

namespace RecurringTests.CategoryTests;

/// <summary>
/// Property 3: MainCategory type change cascades to active subcategories
/// For any MainCategory with active subcategories, changing its Type updates all active children.
/// Inactive subcategories are NOT changed.
/// **Validates: Requirements 5.2**
/// </summary>
[Trait("Feature", "category-type-classification")]
[Trait("Property", "3")]
public class TypeCascadePropertyTests
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
    /// Generates a valid CategoryType enum member (Income, Expense, or Both).
    /// </summary>
    private static Gen<CategoryType> ValidCategoryType()
    {
        return Gen.Elements(CategoryType.Income, CategoryType.Expense, CategoryType.Both);
    }

    /// <summary>
    /// Property 3: For any MainCategory with a mix of active and inactive subcategories,
    /// changing the MainCategory's Type SHALL update all active (non-inactive) subcategories
    /// to match the new type, while leaving inactive subcategories unchanged.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<Property> TypeChange_CascadesToActiveSubcategories_LeavesInactiveUnchanged(
        PositiveInt parentExternalId,
        byte activeCount,
        byte inactiveCount)
    {
        // Constrain counts to keep tests fast
        int numActive = activeCount % 11;    // 0..10 active subcategories
        int numInactive = inactiveCount % 6; // 0..5 inactive subcategories

        // Skip if no subcategories at all (nothing to cascade to)
        if (numActive == 0 && numInactive == 0)
            return true.ToProperty();

        return Prop.ForAll(
            ValidCategoryType().ToArbitrary(),
            ValidCategoryType().ToArbitrary(),
            async (originalType, newType) =>
            {
                var parentExtId = parentExternalId.Get;
                var dbName = $"TypeCascade_{Guid.NewGuid()}";
                var factory = CreateFactory(dbName);

                var beforeChange = DateTime.UtcNow.AddMinutes(-5);

                // Seed the main category and subcategories
                using (var ctx = await factory.CreateDbContextAsync())
                {
                    ctx.Category.Add(new CategoryDTO
                    {
                        CategoryId = Guid.NewGuid(),
                        ExternalId = parentExtId,
                        Name = "MainCategory",
                        IsMainCategory = true,
                        Inactive = false,
                        Type = originalType,
                        UpdatedAt = beforeChange,
                        CreatedAt = beforeChange,
                        UserId = 1,
                        SystemDefault = false,
                    });

                    for (int i = 0; i < numActive; i++)
                    {
                        ctx.Category.Add(new CategoryDTO
                        {
                            CategoryId = Guid.NewGuid(),
                            ExternalId = parentExtId + 100 + i,
                            Name = $"ActiveSub_{i}",
                            IsMainCategory = false,
                            ParentExternalId = parentExtId,
                            Inactive = false,
                            Type = originalType,
                            UpdatedAt = beforeChange,
                            CreatedAt = beforeChange,
                            UserId = 1,
                            SystemDefault = false,
                        });
                    }

                    for (int i = 0; i < numInactive; i++)
                    {
                        ctx.Category.Add(new CategoryDTO
                        {
                            CategoryId = Guid.NewGuid(),
                            ExternalId = parentExtId + 200 + i,
                            Name = $"InactiveSub_{i}",
                            IsMainCategory = false,
                            ParentExternalId = parentExtId,
                            Inactive = true,
                            Type = originalType,
                            UpdatedAt = beforeChange,
                            CreatedAt = beforeChange,
                            UserId = 1,
                            SystemDefault = false,
                        });
                    }

                    await ctx.SaveChangesAsync();
                }

                // Set up real repo + service with mocked API dependencies
                var categoryRepo = new CategoryRepo(factory);
                var categoryApiRepo = Substitute.For<ApiRepo.ICategoryApiRepo>();
                var syncCursorRepo = Substitute.For<ISyncCursorRepo>();
                var service = new CategoryService(categoryRepo, categoryApiRepo, syncCursorRepo);

                // Retrieve the main category
                var all = await service.GetAllAsync();
                var mainCategory = all.First(c => c.IsMainCategory && c.ExternalId == parentExtId);

                // Act: change the MainCategory type
                var timeBeforeAction = DateTime.UtcNow;
                await service.UpdateMainCategoryTypeAsync(mainCategory, newType);

                // Assert: verify final state
                using var verifyCtx = await factory.CreateDbContextAsync();

                var finalMain = await verifyCtx.Category
                    .FirstAsync(c => c.IsMainCategory && c.ExternalId == parentExtId);

                var finalActiveSubs = await verifyCtx.Category
                    .Where(c => !c.IsMainCategory && c.ParentExternalId == parentExtId && !c.Inactive)
                    .ToListAsync();

                var finalInactiveSubs = await verifyCtx.Category
                    .Where(c => !c.IsMainCategory && c.ParentExternalId == parentExtId && c.Inactive)
                    .ToListAsync();

                // Main category has new type
                bool mainTypeCorrect = finalMain.Type == newType;

                // All active subcategories have the new type
                bool activeSubsCorrect = finalActiveSubs.All(s => s.Type == newType);

                // All active subcategories have updated timestamp
                bool activeSubsTimestampUpdated = finalActiveSubs.All(s => s.UpdatedAt >= timeBeforeAction);

                // Inactive subcategories retain the original type (unchanged)
                bool inactiveSubsUnchanged = finalInactiveSubs.All(s => s.Type == originalType);

                // Inactive subcategories retain their original timestamp
                bool inactiveSubsTimestampUnchanged = finalInactiveSubs.All(s => s.UpdatedAt == beforeChange);

                return (mainTypeCorrect
                    && activeSubsCorrect
                    && activeSubsTimestampUpdated
                    && inactiveSubsUnchanged
                    && inactiveSubsTimestampUnchanged)
                    .Label($"MainType={finalMain.Type}(expected {newType}), " +
                           $"ActiveSubs={finalActiveSubs.Count}(all correct={activeSubsCorrect}), " +
                           $"InactiveSubs={finalInactiveSubs.Count}(unchanged={inactiveSubsUnchanged})");
            });
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
