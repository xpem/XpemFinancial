using FsCheck;
using FsCheck.Xunit;
using FinancialService.Model.DTO;
using FinancialService.Model.Req;
using FinancialService.Repo;
using FinancialService.Service;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Property 2: Server Upsert Idempotence
/// Validates: Requirements 4.1, 4.2, 4.3, 4.5
/// </summary>
[Trait("Feature", "category-guid-sync")]
[Trait("Property", "2")]
public class CategoryServerUpsertIdempotencePropertyTests
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
    /// Creates a valid TransactionCategoryReq with the given CategoryId and fields.
    /// </summary>
    private static TransactionCategoryReq CreateReq(Guid categoryId, string name, bool isMain, int? parentId, bool inactive, string? color) => new()
    {
        CategoryId = categoryId,
        Name = name,
        IsMainTransactionCategory = isMain,
        ParentTransactionCategoryId = parentId,
        Inactive = inactive,
        Color = color
    };

    /// <summary>
    /// Generates a valid Name string (1-50 chars, non-empty).
    /// </summary>
    private static string MakeValidName(NonEmptyString nes)
    {
        var raw = nes.Get;
        if (raw.Length > 50)
            return raw[..50];
        return raw;
    }

    /// <summary>
    /// Generates a valid Color string (1-8 chars) or null.
    /// </summary>
    private static string? MakeValidColor(string? raw)
    {
        if (raw is null) return null;
        if (raw.Length > 8) return raw[..8];
        if (raw.Length == 0) return null;
        return raw;
    }

    /// <summary>
    /// Push same CategoryId+UserId N times → exactly one record, same Id returned.
    /// **Validates: Requirements 4.1, 4.2, 4.3, 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> SameCategoryId_PushedNTimes_ExactlyOneRecord_SameIdReturned(
        Guid categoryId,
        PositiveInt userId,
        PositiveInt repeatCount,
        NonEmptyString name)
    {
        if (categoryId == Guid.Empty) return true; // skip trivial case

        int uid = userId.Get;
        int n = Math.Min(repeatCount.Get, 10); // cap at 10 to keep test fast
        string validName = MakeValidName(name);

        var dbName = $"CatServerUpsert_Idempotence_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);
        var repo = new TransactionCategoryRepo(factory);
        var service = new TransactionCategoryService(repo);

        var req = CreateReq(categoryId, validName, false, null, false, null);

        int? firstId = null;
        for (int i = 0; i < n; i++)
        {
            var result = await service.UpsertAsync(req, uid);
            if (firstId == null)
                firstId = result.Id;
            else if (result.Id != firstId)
                return false; // Different Id returned — idempotence violated
        }

        // Verify exactly one record exists with this CategoryId+UserId
        using var ctx = factory.CreateDbContext();
        var count = ctx.TransactionCategory.Count(c => c.CategoryId == categoryId && c.UserId == uid);

        return count == 1;
    }

    /// <summary>
    /// Mutable fields reflect the last request's values after multiple upserts.
    /// **Validates: Requirements 4.1, 4.2, 4.3, 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> MutableFields_ReflectLastRequestValues(
        Guid categoryId,
        PositiveInt userId,
        NonEmptyString firstName,
        NonEmptyString lastName)
    {
        if (categoryId == Guid.Empty) return true; // skip trivial case

        int uid = userId.Get;
        string name1 = MakeValidName(firstName);
        string name2 = MakeValidName(lastName);

        var dbName = $"CatServerUpsert_Mutable_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);
        var repo = new TransactionCategoryRepo(factory);
        var service = new TransactionCategoryService(repo);

        // First push with initial values
        var req1 = CreateReq(categoryId, name1, false, null, false, "FF0000");
        var result1 = await service.UpsertAsync(req1, uid);

        // Second push with different mutable values
        var req2 = CreateReq(categoryId, name2, true, 42, true, "00FF00");
        var result2 = await service.UpsertAsync(req2, uid);

        // Same Id must be returned
        if (result1.Id != result2.Id) return false;

        // Verify mutable fields reflect the LAST request
        using var ctx = factory.CreateDbContext();
        var stored = ctx.TransactionCategory.Single(c => c.CategoryId == categoryId && c.UserId == uid);

        return stored.Name == name2
            && stored.IsMainTransactionCategory == true
            && stored.ParentTransactionCategoryId == 42
            && stored.Inactive == true
            && stored.Color == "00FF00";
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
