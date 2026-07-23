// Feature: category-type-classification, Property 9: Server preserves existing Type when request omits it
using FsCheck;
using FsCheck.Xunit;
using FinancialService.Model.DTO;
using FinancialService.Model.Req;
using FinancialService.Repo;
using FinancialService.Service;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace RecurringTests.CategoryTests;

/// <summary>
/// Property 9: Server preserves existing Type when request omits it
/// For any existing category with Type in {0, 1, 2}, an upsert with Type=null preserves
/// the original Type. For new categories with Type=null, the inserted record gets Type=2.
/// **Validates: Requirements 11.1, 11.3**
/// </summary>
[Trait("Feature", "category-type-classification")]
[Trait("Property", "9")]
public class ServerPreservesTypeOnNullPropertyTests
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
    /// For any existing category with Type in {0, 1, 2}, when an upsert request is received
    /// with Type=null, the server SHALL preserve the category's existing Type value.
    /// **Validates: Requirements 11.1, 11.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> ExistingCategory_NullTypeInRequest_PreservesOriginalType(
        Guid categoryId,
        PositiveInt userId,
        NonEmptyString originalName,
        NonEmptyString updatedName,
        byte typeRaw)
    {
        if (categoryId == Guid.Empty) return true; // skip trivial case

        int uid = userId.Get;
        int existingType = typeRaw % 3; // constrain to valid range {0, 1, 2}
        string name1 = MakeValidName(originalName);
        string name2 = MakeValidName(updatedName);

        var dbName = $"ServerPreservesType_Update_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);
        var repo = new TransactionCategoryRepo(factory);
        var service = new TransactionCategoryService(repo);

        // First upsert: create the category with an explicit Type
        var createReq = new TransactionCategoryReq
        {
            CategoryId = categoryId,
            Name = name1,
            IsMainTransactionCategory = true,
            ParentTransactionCategoryId = null,
            Inactive = false,
            Color = null,
            Type = existingType
        };
        await service.UpsertAsync(createReq, uid);

        // Second upsert: update without providing Type (null) — simulating older client
        var updateReq = new TransactionCategoryReq
        {
            CategoryId = categoryId,
            Name = name2,
            IsMainTransactionCategory = true,
            ParentTransactionCategoryId = null,
            Inactive = false,
            Color = null,
            Type = null // older client omits Type
        };
        await service.UpsertAsync(updateReq, uid);

        // Verify the stored Type is preserved (not overwritten)
        using var ctx = factory.CreateDbContext();
        var stored = ctx.TransactionCategory.Single(c => c.CategoryId == categoryId && c.UserId == uid);

        return stored.Type == existingType;
    }

    /// <summary>
    /// For a new category (not existing on server) with Type=null in the request,
    /// the server SHALL default to Type=2 (Both).
    /// **Validates: Requirements 11.1, 11.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> NewCategory_NullTypeInRequest_DefaultsToTwo(
        Guid categoryId,
        PositiveInt userId,
        NonEmptyString name)
    {
        if (categoryId == Guid.Empty) return true; // skip trivial case

        int uid = userId.Get;
        string validName = MakeValidName(name);

        var dbName = $"ServerPreservesType_Insert_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);
        var repo = new TransactionCategoryRepo(factory);
        var service = new TransactionCategoryService(repo);

        // Upsert a new category with Type=null (older client that doesn't send Type)
        var req = new TransactionCategoryReq
        {
            CategoryId = categoryId,
            Name = validName,
            IsMainTransactionCategory = false,
            ParentTransactionCategoryId = null,
            Inactive = false,
            Color = null,
            Type = null
        };
        await service.UpsertAsync(req, uid);

        // Verify the stored Type defaults to 2 (Both)
        using var ctx = factory.CreateDbContext();
        var stored = ctx.TransactionCategory.Single(c => c.CategoryId == categoryId && c.UserId == uid);

        return stored.Type == 2;
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
