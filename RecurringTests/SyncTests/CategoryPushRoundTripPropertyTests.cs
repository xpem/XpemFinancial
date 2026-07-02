using ApiRepo;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Model.Req;
using Model.Resp.Api;
using NSubstitute;
using Repo;
using Service.Category;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Property 3: Push Round-Trip Preserves Identity
/// Validates: Requirements 3.1, 3.2, 6.3
/// </summary>
[Trait("Feature", "category-guid-sync")]
[Trait("Property", "3")]
public class CategoryPushRoundTripPropertyTests
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
    /// For any category with a non-empty CategoryId and null ExternalId,
    /// when PushAsync is called, the CategoryReq sent to PostCategoryAsync
    /// SHALL contain the local CategoryId.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PushIncludesCategoryIdInRequest(Guid categoryId, NonEmptyString name)
    {
        if (categoryId == Guid.Empty) return true; // skip trivial case

        var dbName = $"CatPushRoundTrip_IncludesId_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed a category with a valid CategoryId and null ExternalId (pending push)
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Category.Add(new CategoryDTO
            {
                CategoryId = categoryId,
                ExternalId = null,
                Name = name.Get,
                IsMainCategory = true,
                UserId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        // Setup mocks
        var categoryApiRepo = Substitute.For<ICategoryApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        // Capture the request sent to PostCategoryAsync and return a valid Id
        CategoryReq? capturedReq = null;
        categoryApiRepo.PostCategoryAsync(Arg.Do<CategoryReq>(r => capturedReq = r))
            .Returns(new CategoryPushRes { Id = 42 });

        var categoryRepo = new CategoryRepo(factory);
        var service = new CategoryService(categoryRepo, categoryApiRepo, syncCursorRepo);

        await service.PushAsync();

        // Verify: the captured request contains the correct CategoryId
        return capturedReq is not null && capturedReq.CategoryId == categoryId;
    }

    /// <summary>
    /// For any category with a non-empty CategoryId, when the server responds
    /// with a valid Id (> 0), the local record SHALL have ExternalId set to that value.
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> SuccessfulPushPersistsExternalId(Guid categoryId, PositiveInt serverId, NonEmptyString name)
    {
        if (categoryId == Guid.Empty) return true; // skip trivial case

        var serverIdValue = serverId.Get;
        var dbName = $"CatPushRoundTrip_ExternalId_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed a category with valid CategoryId and null ExternalId
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Category.Add(new CategoryDTO
            {
                CategoryId = categoryId,
                ExternalId = null,
                Name = name.Get,
                IsMainCategory = false,
                UserId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        // Setup mocks
        var categoryApiRepo = Substitute.For<ICategoryApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        // PostCategoryAsync returns the serverId
        categoryApiRepo.PostCategoryAsync(Arg.Any<CategoryReq>())
            .Returns(new CategoryPushRes { Id = serverIdValue });

        var categoryRepo = new CategoryRepo(factory);
        var service = new CategoryService(categoryRepo, categoryApiRepo, syncCursorRepo);

        await service.PushAsync();

        // Verify: the local record now has ExternalId = serverId
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var record = await ctx.Category
                .FirstOrDefaultAsync(c => c.CategoryId == categoryId);

            return record is not null && record.ExternalId == serverIdValue;
        }
    }

    /// <summary>
    /// For any category that has been successfully pushed (ExternalId set),
    /// it SHALL be excluded from subsequent GetPendingPushAsync calls.
    /// **Validates: Requirements 6.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PushedRecordExcludedFromSubsequentPending(Guid categoryId, PositiveInt serverId, NonEmptyString name)
    {
        if (categoryId == Guid.Empty) return true; // skip trivial case

        var serverIdValue = serverId.Get;
        var dbName = $"CatPushRoundTrip_Excluded_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed a category with valid CategoryId and null ExternalId
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Category.Add(new CategoryDTO
            {
                CategoryId = categoryId,
                ExternalId = null,
                Name = name.Get,
                IsMainCategory = true,
                UserId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        // Setup mocks
        var categoryApiRepo = Substitute.For<ICategoryApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        // PostCategoryAsync returns the serverId
        categoryApiRepo.PostCategoryAsync(Arg.Any<CategoryReq>())
            .Returns(new CategoryPushRes { Id = serverIdValue });

        var categoryRepo = new CategoryRepo(factory);
        var service = new CategoryService(categoryRepo, categoryApiRepo, syncCursorRepo);

        // First push — should push the record and persist ExternalId
        await service.PushAsync();

        // Second call to GetPendingPushAsync — should NOT include the pushed record
        var pendingAfterPush = await categoryRepo.GetPendingPushAsync();

        return !pendingAfterPush.Any(c => c.CategoryId == categoryId);
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
