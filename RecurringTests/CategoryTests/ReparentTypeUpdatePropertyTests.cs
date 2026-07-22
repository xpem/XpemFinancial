// Feature: category-type-classification, Property 4: Re-parenting subcategory updates its Type
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
/// Property-based tests for re-parenting subcategory updating its Type.
/// For any subcategory moved to a new parent, its Type matches the new parent's Type.
/// **Validates: Requirements 5.3**
/// </summary>
[Trait("Feature", "category-type-classification")]
[Trait("Property", "4")]
public class ReparentTypeUpdatePropertyTests
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
    /// Property 4: For any subcategory with any initial Type, when re-parented to a
    /// new parent with a (potentially different) Type, after UpdateLocalAsync the
    /// subcategory's Type matches the new parent's Type.
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ReparentedSubcategory_TypeMatchesNewParent()
    {
        var gen = from initialSubType in ValidCategoryType()
                  from originalParentType in ValidCategoryType()
                  from newParentType in ValidCategoryType()
                  from subcategoryName in Gen.Elements("Sub1", "Sub2", "Sub3", "MySub")
                  select (initialSubType, originalParentType, newParentType, subcategoryName);

        return Prop.ForAll(
            gen.ToArbitrary(),
            async tuple =>
            {
                var (initialSubType, originalParentType, newParentType, subcategoryName) = tuple;

                var dbName = $"ReparentType_{Guid.NewGuid()}";
                var factory = CreateFactory(dbName);
                var now = DateTime.UtcNow;

                const int originalParentExtId = 100;
                const int newParentExtId = 200;
                var subcategoryId = Guid.NewGuid();

                // Seed: original parent, new parent, and subcategory under original parent
                using (var ctx = await factory.CreateDbContextAsync())
                {
                    ctx.Category.Add(new CategoryDTO
                    {
                        CategoryId = Guid.NewGuid(),
                        ExternalId = originalParentExtId,
                        Name = "OriginalParent",
                        IsMainCategory = true,
                        Inactive = false,
                        Type = originalParentType,
                        UpdatedAt = now,
                        CreatedAt = now,
                        UserId = 1,
                        SystemDefault = false,
                    });

                    ctx.Category.Add(new CategoryDTO
                    {
                        CategoryId = Guid.NewGuid(),
                        ExternalId = newParentExtId,
                        Name = "NewParent",
                        IsMainCategory = true,
                        Inactive = false,
                        Type = newParentType,
                        UpdatedAt = now,
                        CreatedAt = now,
                        UserId = 1,
                        SystemDefault = false,
                    });

                    ctx.Category.Add(new CategoryDTO
                    {
                        CategoryId = subcategoryId,
                        ExternalId = 300,
                        Name = subcategoryName,
                        IsMainCategory = false,
                        ParentExternalId = originalParentExtId,
                        Inactive = false,
                        Type = initialSubType,
                        UpdatedAt = now,
                        CreatedAt = now,
                        UserId = 1,
                        SystemDefault = false,
                    });

                    await ctx.SaveChangesAsync();
                }

                // Arrange: create service with real repo
                var categoryRepo = new CategoryRepo(factory);
                var categoryApiRepo = Substitute.For<ApiRepo.ICategoryApiRepo>();
                var syncCursorRepo = Substitute.For<ISyncCursorRepo>();
                var service = new CategoryService(categoryRepo, categoryApiRepo, syncCursorRepo);

                // Act: re-parent subcategory to new parent
                using (var ctx = await factory.CreateDbContextAsync())
                {
                    var subcategory = await ctx.Category.FirstAsync(c => c.CategoryId == subcategoryId);
                    subcategory.ParentExternalId = newParentExtId;

                    // Detach so the service uses its own context
                    ctx.Entry(subcategory).State = EntityState.Detached;

                    await service.UpdateLocalAsync(subcategory);
                }

                // Assert: subcategory's Type now matches the new parent's Type
                using (var ctx = await factory.CreateDbContextAsync())
                {
                    var updated = await ctx.Category.FirstAsync(c => c.CategoryId == subcategoryId);
                    return (updated.Type == newParentType)
                        .ToProperty()
                        .Label($"Expected Type={newParentType} (from new parent) but got Type={updated.Type}. " +
                               $"Initial sub type was {initialSubType}, original parent type was {originalParentType}");
                }
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
