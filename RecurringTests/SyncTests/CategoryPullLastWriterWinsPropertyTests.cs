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
/// Property 4: Pull CategoryId Matching with Last-Writer-Wins
/// Validates: Requirements 5.1, 5.2, 1.3, 7.3
/// </summary>
[Trait("Feature", "category-guid-sync")]
[Trait("Property", "4")]
public class CategoryPullLastWriterWinsPropertyTests
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
    /// For any local category with a non-empty CategoryId,
    /// when a pulled category with the same CategoryId has UpdatedAt strictly greater
    /// than the local UpdatedAt, the local record SHALL be updated with the pulled data.
    /// **Validates: Requirements 5.1, 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PulledNewerUpdatedAt_UpdatesLocalRecord(Guid categoryId, PositiveInt ticksOffset)
    {
        if (categoryId == Guid.Empty) return true; // skip trivial case

        var dbName = $"CatPullLWW_Newer_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var localUpdatedAt = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        // Pulled UpdatedAt is strictly greater (add positive offset in ticks)
        var pulledUpdatedAt = localUpdatedAt.AddTicks(ticksOffset.Get);

        var userId = 1;
        var externalId = 42;

        // Seed local category
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Category.Add(new CategoryDTO
            {
                CategoryId = categoryId,
                ExternalId = externalId,
                Name = "Original local",
                IsMainCategory = true,
                UserId = userId,
                SystemDefault = false,
                Inactive = false,
                UpdatedAt = localUpdatedAt,
                CreatedAt = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        // Build pulled category response with same CategoryId but newer UpdatedAt
        var pulledCategories = new List<TransactionCategoryApiRes>
        {
            new()
            {
                Id = externalId,
                CategoryId = categoryId,
                Name = "Updated from server",
                IsMainTransactionCategory = false,
                SystemDefault = true,
                Inactive = true,
                ParentTransactionCategoryId = 5,
                UpdatedAt = pulledUpdatedAt,
            }
        };

        // Setup service with real repo, mocked API dependencies
        var categoryApiRepo = Substitute.For<ICategoryApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        categoryApiRepo.GetByLastUpdateAsync(Arg.Any<DateTime>(), page: 1)
            .Returns(new ApiResp
            {
                Success = true,
                Content = JsonSerializer.Serialize(pulledCategories, _jsonOptions)
            });

        syncCursorRepo.GetAsync(SyncCursorKeys.Category).Returns(DateTime.MinValue);

        var categoryRepo = new CategoryRepo(factory);
        var service = new CategoryService(categoryRepo, categoryApiRepo, syncCursorRepo);

        await service.PullAsync(userId, localUpdatedAt);

        // Verify: local record was updated
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var record = await ctx.Category
                .FirstOrDefaultAsync(c => c.CategoryId == categoryId);

            return record is not null
                && record.Name == "Updated from server"
                && record.IsMainCategory == false
                && record.SystemDefault == true
                && record.Inactive == true
                && record.ParentExternalId == 5
                && record.UpdatedAt == pulledUpdatedAt;
        }
    }

    /// <summary>
    /// For any local category with a non-empty CategoryId,
    /// when a pulled category with the same CategoryId has UpdatedAt less than or equal
    /// to the local UpdatedAt, the local record SHALL NOT be updated.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PulledOlderOrEqualUpdatedAt_DoesNotUpdateLocalRecord(Guid categoryId, NonNegativeInt ticksOffset)
    {
        if (categoryId == Guid.Empty) return true; // skip trivial case

        var dbName = $"CatPullLWW_OlderEq_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var localUpdatedAt = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        // Pulled UpdatedAt is less than or equal (subtract offset ticks from local)
        var pulledUpdatedAt = localUpdatedAt.AddTicks(-ticksOffset.Get);

        var userId = 1;
        var externalId = 42;

        // Seed local category
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Category.Add(new CategoryDTO
            {
                CategoryId = categoryId,
                ExternalId = externalId,
                Name = "Original local",
                IsMainCategory = true,
                UserId = userId,
                SystemDefault = false,
                Inactive = false,
                UpdatedAt = localUpdatedAt,
                CreatedAt = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        // Build pulled category response with same CategoryId but older/equal UpdatedAt
        var pulledCategories = new List<TransactionCategoryApiRes>
        {
            new()
            {
                Id = externalId,
                CategoryId = categoryId,
                Name = "Should not overwrite",
                IsMainTransactionCategory = false,
                SystemDefault = true,
                Inactive = true,
                ParentTransactionCategoryId = 99,
                UpdatedAt = pulledUpdatedAt,
            }
        };

        // Setup service with real repo, mocked API dependencies
        var categoryApiRepo = Substitute.For<ICategoryApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        categoryApiRepo.GetByLastUpdateAsync(Arg.Any<DateTime>(), page: 1)
            .Returns(new ApiResp
            {
                Success = true,
                Content = JsonSerializer.Serialize(pulledCategories, _jsonOptions)
            });

        syncCursorRepo.GetAsync(SyncCursorKeys.Category).Returns(DateTime.MinValue);

        var categoryRepo = new CategoryRepo(factory);
        var service = new CategoryService(categoryRepo, categoryApiRepo, syncCursorRepo);

        await service.PullAsync(userId, localUpdatedAt);

        // Verify: local record was NOT updated
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var record = await ctx.Category
                .FirstOrDefaultAsync(c => c.CategoryId == categoryId);

            return record is not null
                && record.Name == "Original local"
                && record.IsMainCategory == true
                && record.SystemDefault == false
                && record.Inactive == false
                && record.UpdatedAt == localUpdatedAt;
        }
    }

    /// <summary>
    /// For any pulled category with a non-empty CategoryId that matches a local record,
    /// after update the local record's CategoryId SHALL equal the pulled value.
    /// **Validates: Requirements 1.3, 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PulledCategoryId_IsPreservedOnLocalRecord(Guid categoryId, PositiveInt ticksOffset)
    {
        if (categoryId == Guid.Empty) return true; // skip trivial case

        var dbName = $"CatPullLWW_Preserved_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var localUpdatedAt = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var pulledUpdatedAt = localUpdatedAt.AddTicks(ticksOffset.Get);

        var userId = 1;
        var externalId = 42;

        // Seed local category with the same CategoryId
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Category.Add(new CategoryDTO
            {
                CategoryId = categoryId,
                ExternalId = externalId,
                Name = "Local record",
                IsMainCategory = true,
                UserId = userId,
                SystemDefault = false,
                Inactive = false,
                UpdatedAt = localUpdatedAt,
                CreatedAt = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        // Build pulled category with same CategoryId and newer UpdatedAt
        var pulledCategories = new List<TransactionCategoryApiRes>
        {
            new()
            {
                Id = externalId,
                CategoryId = categoryId,
                Name = "From server",
                IsMainTransactionCategory = false,
                SystemDefault = true,
                Inactive = false,
                ParentTransactionCategoryId = null,
                UpdatedAt = pulledUpdatedAt,
            }
        };

        var categoryApiRepo = Substitute.For<ICategoryApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        categoryApiRepo.GetByLastUpdateAsync(Arg.Any<DateTime>(), page: 1)
            .Returns(new ApiResp
            {
                Success = true,
                Content = JsonSerializer.Serialize(pulledCategories, _jsonOptions)
            });

        syncCursorRepo.GetAsync(SyncCursorKeys.Category).Returns(DateTime.MinValue);

        var categoryRepo = new CategoryRepo(factory);
        var service = new CategoryService(categoryRepo, categoryApiRepo, syncCursorRepo);

        await service.PullAsync(userId, localUpdatedAt);

        // Verify: CategoryId is preserved on local record
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var record = await ctx.Category
                .FirstOrDefaultAsync(c => c.CategoryId == categoryId);

            return record is not null && record.CategoryId == categoryId;
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
