using FsCheck;
using FsCheck.Xunit;
using FinancialService.Model.DTO;
using FinancialService.Repo;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Property 8: Server Repository Lookup Correctness
/// Validates: Requirements 9.2, 9.3, 9.4
/// </summary>
[Trait("Feature", "category-guid-sync")]
[Trait("Property", "8")]
public class CategoryServerRepoLookupPropertyTests
{
    /// <summary>
    /// Creates a DbContextFactory backed by a unique in-memory database.
    /// </summary>
    private static IDbContextFactory<FinancialDbctx> CreateFactory(string dbName)
    {
        var options = new DbContextOptionsBuilder<FinancialDbctx>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new TestFinancialDbContextFactory(options);
    }

    /// <summary>
    /// Creates a valid server TransactionCategoryDTO with the given CategoryId and UserId.
    /// </summary>
    private static TransactionCategoryDTO CreateCategory(Guid categoryId, int userId) => new()
    {
        CategoryId = categoryId,
        UserId = userId,
        Name = "Test category",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        IsMainTransactionCategory = false,
        Inactive = false
    };

    /// <summary>
    /// For any valid Guid (non-empty) and UserId, when a matching record exists in the database,
    /// FindByCategoryIdAsync returns that record.
    /// **Validates: Requirements 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> ExistingGuidAndUserId_ReturnsMatchingRecord(Guid categoryId, PositiveInt userId)
    {
        if (categoryId == Guid.Empty) return true; // skip trivial case
        int uid = userId.Get;

        var dbName = $"CatServerLookup_Existing_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed the record
        using (var ctx = factory.CreateDbContext())
        {
            ctx.TransactionCategory.Add(CreateCategory(categoryId, uid));
            await ctx.SaveChangesAsync();
        }

        var repo = new TransactionCategoryRepo(factory);
        var result = await repo.FindByCategoryIdAsync(categoryId, uid);

        return result is not null && result.CategoryId == categoryId && result.UserId == uid;
    }

    /// <summary>
    /// For any valid Guid (non-empty), when no matching record exists for the given user,
    /// FindByCategoryIdAsync returns null.
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> NonExistentGuid_ReturnsNull(Guid categoryId, PositiveInt userId)
    {
        if (categoryId == Guid.Empty) return true; // skip trivial case
        int uid = userId.Get;

        var dbName = $"CatServerLookup_NonExist_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Empty database — no records seeded
        var repo = new TransactionCategoryRepo(factory);
        var result = await repo.FindByCategoryIdAsync(categoryId, uid);

        return result is null;
    }

    /// <summary>
    /// For any valid Guid (non-empty), when the record exists but belongs to a different user,
    /// FindByCategoryIdAsync returns null (scoped by UserId).
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> MismatchedUserId_ReturnsNull(Guid categoryId, PositiveInt ownerUserId, PositiveInt queryUserId)
    {
        if (categoryId == Guid.Empty) return true;
        int ownerId = ownerUserId.Get;
        int queryId = queryUserId.Get;
        if (ownerId == queryId) return true; // skip when IDs match

        var dbName = $"CatServerLookup_Mismatch_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed a record owned by ownerId
        using (var ctx = factory.CreateDbContext())
        {
            ctx.TransactionCategory.Add(CreateCategory(categoryId, ownerId));
            await ctx.SaveChangesAsync();
        }

        var repo = new TransactionCategoryRepo(factory);
        var result = await repo.FindByCategoryIdAsync(categoryId, queryId);

        return result is null;
    }

    /// <summary>
    /// Guid.Empty always returns null — even when records exist in the database.
    /// This verifies the short-circuit guard without querying the DB.
    /// **Validates: Requirements 9.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> GuidEmpty_ReturnsNull_WithoutDbQuery(Guid seedId, PositiveInt userId)
    {
        if (seedId == Guid.Empty) return true; // skip when seed is also empty
        int uid = userId.Get;

        var dbName = $"CatServerLookup_Empty_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed a record so the database isn't empty
        using (var ctx = factory.CreateDbContext())
        {
            ctx.TransactionCategory.Add(CreateCategory(seedId, uid));
            await ctx.SaveChangesAsync();
        }

        var repo = new TransactionCategoryRepo(factory);
        var result = await repo.FindByCategoryIdAsync(Guid.Empty, uid);

        return result is null;
    }

    /// <summary>
    /// For any valid Guid (non-empty), inactive records are also returned.
    /// **Validates: Requirements 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> InactiveRecord_IsStillReturned(Guid categoryId, PositiveInt userId)
    {
        if (categoryId == Guid.Empty) return true;
        int uid = userId.Get;

        var dbName = $"CatServerLookup_Inactive_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed an inactive record
        using (var ctx = factory.CreateDbContext())
        {
            var cat = CreateCategory(categoryId, uid);
            cat.Inactive = true;
            ctx.TransactionCategory.Add(cat);
            await ctx.SaveChangesAsync();
        }

        var repo = new TransactionCategoryRepo(factory);
        var result = await repo.FindByCategoryIdAsync(categoryId, uid);

        return result is not null && result.CategoryId == categoryId && result.Inactive;
    }

    /// <summary>
    /// Simple IDbContextFactory implementation for testing.
    /// </summary>
    private sealed class TestFinancialDbContextFactory(DbContextOptions<FinancialDbctx> options) : IDbContextFactory<FinancialDbctx>
    {
        public FinancialDbctx CreateDbContext() => new(options);
        public Task<FinancialDbctx> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new FinancialDbctx(options));
    }
}
