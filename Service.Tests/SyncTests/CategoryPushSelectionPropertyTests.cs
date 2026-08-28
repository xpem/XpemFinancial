using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Repo;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Property 6: Push Selection Criteria
/// Validates: Requirements 6.2, 7.5
/// </summary>
[Trait("Feature", "category-guid-sync")]
[Trait("Property", "6")]
public class CategoryPushSelectionPropertyTests
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
    /// Creates a valid CategoryDTO with specified CategoryId and ExternalId.
    /// </summary>
    private static CategoryDTO CreateCategory(Guid categoryId, int? externalId, string name = "Test") => new()
    {
        CategoryId = categoryId,
        ExternalId = externalId,
        Name = name,
        UserId = 1,
        IsMainCategory = false,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    /// <summary>
    /// For any category with CategoryId != Guid.Empty AND ExternalId == null,
    /// GetPendingPushAsync includes that record in the result.
    /// **Validates: Requirements 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PendingCategory_IsIncludedInPush(Guid categoryId)
    {
        if (categoryId == Guid.Empty) return true; // skip trivial case

        var dbName = $"PushSelection_Included_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed a category that should be pending push
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Category.Add(CreateCategory(categoryId, externalId: null));
            await ctx.SaveChangesAsync();
        }

        var repo = new CategoryRepo(factory);
        var pending = await repo.GetPendingPushAsync();

        return pending.Count == 1 && pending[0].CategoryId == categoryId;
    }

    /// <summary>
    /// For any category with CategoryId == Guid.Empty (regardless of ExternalId),
    /// GetPendingPushAsync excludes that record.
    /// **Validates: Requirements 7.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> EmptyGuidCategory_IsExcludedFromPush(int? externalId)
    {
        var dbName = $"PushSelection_EmptyGuid_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed a category with Guid.Empty — should never be pending
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Category.Add(CreateCategory(Guid.Empty, externalId));
            await ctx.SaveChangesAsync();
        }

        var repo = new CategoryRepo(factory);
        var pending = await repo.GetPendingPushAsync();

        return pending.Count == 0;
    }

    /// <summary>
    /// For any category with a valid CategoryId and a valid ExternalId (> 0),
    /// GetPendingPushAsync excludes that record (already pushed).
    /// **Validates: Requirements 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> AlreadyPushedCategory_IsExcludedFromPush(Guid categoryId, PositiveInt externalId)
    {
        if (categoryId == Guid.Empty) return true; // skip trivial case

        var dbName = $"PushSelection_AlreadyPushed_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed a category with valid CategoryId AND valid ExternalId — already pushed
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Category.Add(CreateCategory(categoryId, externalId: externalId.Get));
            await ctx.SaveChangesAsync();
        }

        var repo = new CategoryRepo(factory);
        var pending = await repo.GetPendingPushAsync();

        return pending.Count == 0;
    }

    /// <summary>
    /// Given a mixed set of categories in various states, GetPendingPushAsync returns
    /// exactly those with CategoryId != Guid.Empty AND ExternalId == null.
    /// **Validates: Requirements 6.2, 7.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> MixedCategories_OnlyPendingAreReturned(Guid validGuid1, Guid validGuid2, PositiveInt extId)
    {
        if (validGuid1 == Guid.Empty || validGuid2 == Guid.Empty) return true; // skip trivial

        var dbName = $"PushSelection_Mixed_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        using (var ctx = await factory.CreateDbContextAsync())
        {
            // Pending: valid Guid, no ExternalId
            ctx.Category.Add(CreateCategory(validGuid1, externalId: null, name: "Pending1"));
            // Already pushed: valid Guid, has ExternalId
            ctx.Category.Add(CreateCategory(validGuid2, externalId: extId.Get, name: "Pushed"));
            // Legacy: Guid.Empty, no ExternalId
            ctx.Category.Add(CreateCategory(Guid.Empty, externalId: null, name: "Legacy1"));
            // Legacy pushed: Guid.Empty, has ExternalId
            ctx.Category.Add(CreateCategory(Guid.Empty, externalId: extId.Get + 1, name: "Legacy2"));
            await ctx.SaveChangesAsync();
        }

        var repo = new CategoryRepo(factory);
        var pending = await repo.GetPendingPushAsync();

        // Only the first record (valid Guid + null ExternalId) should be returned
        return pending.Count == 1
            && pending[0].CategoryId == validGuid1
            && pending[0].ExternalId == null;
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
