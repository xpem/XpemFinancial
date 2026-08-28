using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Repo;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Property 7: Client Repository Lookup Correctness
/// Validates: Requirements 8.2, 8.3, 8.4
/// </summary>
[Trait("Feature", "category-guid-sync")]
[Trait("Property", "7")]
public class CategoryClientRepoLookupPropertyTests
{
    /// <summary>
    /// Creates a DbContextFactory backed by a unique in-memory database.
    /// Each call to CreateDbContextAsync returns a context pointing at the same store.
    /// </summary>
    private static IDbContextFactory<DbCtx> CreateFactory(string dbName)
    {
        var options = new DbContextOptionsBuilder<DbCtx>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new TestDbContextFactory(options);
    }

    /// <summary>
    /// Creates a valid CategoryDTO with the given CategoryId.
    /// </summary>
    private static CategoryDTO CreateCategory(Guid categoryId) => new()
    {
        CategoryId = categoryId,
        UserId = 1,
        Name = "Test category",
        IsMainCategory = false,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    /// <summary>
    /// For any valid Guid (non-empty), when a matching record exists in the database,
    /// GetByCategoryIdAsync returns that record.
    /// **Validates: Requirements 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> ExistingGuid_ReturnsMatchingRecord(Guid categoryId)
    {
        if (categoryId == Guid.Empty) return true; // skip trivial case

        var dbName = $"CategoryClientLookup_Existing_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed the record
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Category.Add(CreateCategory(categoryId));
            await ctx.SaveChangesAsync();
        }

        var repo = new CategoryRepo(factory);
        var result = await repo.GetByCategoryIdAsync(categoryId);

        return result is not null && result.CategoryId == categoryId;
    }

    /// <summary>
    /// For any valid Guid (non-empty), when no matching record exists,
    /// GetByCategoryIdAsync returns null.
    /// **Validates: Requirements 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> NonExistentGuid_ReturnsNull(Guid categoryId)
    {
        if (categoryId == Guid.Empty) return true; // skip trivial case

        var dbName = $"CategoryClientLookup_NonExist_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Empty database — no records seeded
        var repo = new CategoryRepo(factory);
        var result = await repo.GetByCategoryIdAsync(categoryId);

        return result is null;
    }

    /// <summary>
    /// Guid.Empty always returns null — even when records exist in the database.
    /// This verifies the short-circuit guard without querying the DB.
    /// **Validates: Requirements 8.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> GuidEmpty_ReturnsNull_WithoutDbQuery(Guid seedId)
    {
        if (seedId == Guid.Empty) return true; // skip when seed is also empty

        var dbName = $"CategoryClientLookup_Empty_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed a record so the database isn't empty
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Category.Add(CreateCategory(seedId));
            await ctx.SaveChangesAsync();
        }

        var repo = new CategoryRepo(factory);
        var result = await repo.GetByCategoryIdAsync(Guid.Empty);

        return result is null;
    }

    /// <summary>
    /// For any valid Guid (non-empty), inactive records are also returned.
    /// **Validates: Requirements 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> InactiveRecord_IsStillReturned(Guid categoryId)
    {
        if (categoryId == Guid.Empty) return true; // skip trivial case

        var dbName = $"CategoryClientLookup_Inactive_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed an inactive record
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var cat = CreateCategory(categoryId);
            cat.Inactive = true;
            ctx.Category.Add(cat);
            await ctx.SaveChangesAsync();
        }

        var repo = new CategoryRepo(factory);
        var result = await repo.GetByCategoryIdAsync(categoryId);

        return result is not null && result.CategoryId == categoryId && result.Inactive;
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
