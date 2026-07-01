using ApiRepo;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Model.Resp.Api;
using NSubstitute;
using Repo;
using Service.Category;
using System.Text.Json;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Property 5: Pull Fallback and Insert
/// Validates: Requirements 5.3, 5.4, 5.5
/// </summary>
[Trait("Feature", "category-guid-sync")]
[Trait("Property", "5")]
public class CategoryPullFallbackInsertPropertyTests
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

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
    /// For any pulled category whose CategoryId does not match any local record,
    /// the system SHALL fall back to matching by ExternalId. If a local record
    /// is found by ExternalId and pulled UpdatedAt is newer, the local record
    /// SHALL be updated with pulled data.
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> FallbackToExternalId_WhenCategoryIdDoesNotMatch(
        Guid pulledCategoryId, PositiveInt externalId, NonEmptyString pulledName)
    {
        if (pulledCategoryId == Guid.Empty) return true; // skip trivial case

        var dbName = $"CatPullFallback_ExternalId_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var externalIdValue = externalId.Get;
        var localUpdatedAt = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var pulledUpdatedAt = localUpdatedAt.AddHours(1); // newer, so update applies
        var userId = 1;

        // Use a different local CategoryId so the CategoryId match will NOT find it
        var localCategoryId = Guid.NewGuid();

        // Seed a local record with a different CategoryId but matching ExternalId
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Category.Add(new CategoryDTO
            {
                CategoryId = localCategoryId,
                ExternalId = externalIdValue,
                Name = "Original local name",
                IsMainCategory = false,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = localUpdatedAt,
            });
            await ctx.SaveChangesAsync();
        }

        // Build the API response: CategoryId doesn't match local, but ExternalId does
        var apiCategories = new List<TransactionCategoryApiRes>
        {
            new()
            {
                Id = externalIdValue,
                CategoryId = pulledCategoryId,
                Name = pulledName.Get,
                UpdatedAt = pulledUpdatedAt,
                SystemDefault = false,
                Inactive = false,
                IsMainTransactionCategory = true,
                ParentTransactionCategoryId = null,
            }
        };

        var categoryApiRepo = Substitute.For<ICategoryApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        categoryApiRepo.GetByLastUpdateAsync(Arg.Any<DateTime>(), Arg.Any<int>())
            .Returns(new ApiResp
            {
                Success = true,
                Content = JsonSerializer.Serialize(apiCategories)
            });

        syncCursorRepo.GetAsync(SyncCursorKeys.Category).Returns(DateTime.MinValue);

        var categoryRepo = new CategoryRepo(factory);
        var service = new CategoryService(categoryRepo, categoryApiRepo, syncCursorRepo);

        await service.PullAsync(userId, DateTime.MinValue);

        // Verify: local record was updated via ExternalId fallback
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var record = await ctx.Category
                .FirstOrDefaultAsync(c => c.ExternalId == externalIdValue);

            return record is not null
                && record.Name == pulledName.Get
                && record.IsMainCategory == true
                && record.CategoryId == pulledCategoryId
                && record.UpdatedAt == pulledUpdatedAt;
        }
    }

    /// <summary>
    /// For any pulled category whose CategoryId does not match any local record
    /// AND whose ExternalId does not match any local record, a new local record
    /// SHALL be inserted.
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> InsertNewRecord_WhenNeitherCategoryIdNorExternalIdMatch(
        Guid pulledCategoryId, PositiveInt externalId, NonEmptyString pulledName)
    {
        if (pulledCategoryId == Guid.Empty) return true; // skip trivial case

        var dbName = $"CatPullFallback_Insert_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var externalIdValue = externalId.Get;
        var pulledUpdatedAt = new DateTime(2024, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var userId = 1;

        // Do NOT seed any local record — no match by CategoryId or ExternalId

        // Build the API response with a CategoryId and ExternalId that won't match anything local
        var apiCategories = new List<TransactionCategoryApiRes>
        {
            new()
            {
                Id = externalIdValue,
                CategoryId = pulledCategoryId,
                Name = pulledName.Get,
                UpdatedAt = pulledUpdatedAt,
                SystemDefault = true,
                Inactive = false,
                IsMainTransactionCategory = false,
                ParentTransactionCategoryId = 99,
            }
        };

        var categoryApiRepo = Substitute.For<ICategoryApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        categoryApiRepo.GetByLastUpdateAsync(Arg.Any<DateTime>(), Arg.Any<int>())
            .Returns(new ApiResp
            {
                Success = true,
                Content = JsonSerializer.Serialize(apiCategories)
            });

        syncCursorRepo.GetAsync(SyncCursorKeys.Category).Returns(DateTime.MinValue);

        var categoryRepo = new CategoryRepo(factory);
        var service = new CategoryService(categoryRepo, categoryApiRepo, syncCursorRepo);

        await service.PullAsync(userId, DateTime.MinValue);

        // Verify: a new record was inserted with the pulled data
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var record = await ctx.Category
                .FirstOrDefaultAsync(c => c.CategoryId == pulledCategoryId);

            return record is not null
                && record.ExternalId == externalIdValue
                && record.Name == pulledName.Get
                && record.SystemDefault == true
                && record.IsMainCategory == false
                && record.ParentExternalId == 99
                && record.UserId == userId
                && record.UpdatedAt == pulledUpdatedAt;
        }
    }

    /// <summary>
    /// For any pulled category whose CategoryId is null or empty, the system
    /// SHALL match by ExternalId only, preserving backward-compatible behavior.
    /// **Validates: Requirements 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> ExternalIdOnlyMatching_WhenCategoryIdIsNullOrEmpty(
        PositiveInt externalId, NonEmptyString pulledName, bool useNull)
    {
        var dbName = $"CatPullFallback_NullCatId_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var externalIdValue = externalId.Get;
        var localUpdatedAt = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var pulledUpdatedAt = localUpdatedAt.AddHours(1); // newer, so update applies
        var userId = 1;

        // Seed a local record with a valid ExternalId (and some CategoryId)
        var localCategoryId = Guid.NewGuid();
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Category.Add(new CategoryDTO
            {
                CategoryId = localCategoryId,
                ExternalId = externalIdValue,
                Name = "Original name",
                IsMainCategory = true,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = localUpdatedAt,
            });
            await ctx.SaveChangesAsync();
        }

        // Build API response with null or empty CategoryId
        Guid? pulledCategoryId = useNull ? null : Guid.Empty;

        var apiCategories = new List<TransactionCategoryApiRes>
        {
            new()
            {
                Id = externalIdValue,
                CategoryId = pulledCategoryId,
                Name = pulledName.Get,
                UpdatedAt = pulledUpdatedAt,
                SystemDefault = false,
                Inactive = false,
                IsMainTransactionCategory = false,
                ParentTransactionCategoryId = null,
            }
        };

        var categoryApiRepo = Substitute.For<ICategoryApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        categoryApiRepo.GetByLastUpdateAsync(Arg.Any<DateTime>(), Arg.Any<int>())
            .Returns(new ApiResp
            {
                Success = true,
                Content = JsonSerializer.Serialize(apiCategories)
            });

        syncCursorRepo.GetAsync(SyncCursorKeys.Category).Returns(DateTime.MinValue);

        var categoryRepo = new CategoryRepo(factory);
        var service = new CategoryService(categoryRepo, categoryApiRepo, syncCursorRepo);

        await service.PullAsync(userId, DateTime.MinValue);

        // Verify: local record was matched by ExternalId and updated
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var record = await ctx.Category
                .FirstOrDefaultAsync(c => c.ExternalId == externalIdValue);

            return record is not null
                && record.Name == pulledName.Get
                && record.UpdatedAt == pulledUpdatedAt
                // CategoryId should remain unchanged since pulled CategoryId is null/empty
                && record.CategoryId == localCategoryId;
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
