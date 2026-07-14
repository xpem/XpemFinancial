// Feature: category-management, Property 9: Push outcome correctly updates ExternalId
using ApiRepo;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Model.Req;
using Model.Resp.Api;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Repo;
using Service.Category;
using Xunit;

namespace RecurringTests.CategoryTests;

/// <summary>
/// Property 9: Push outcome correctly updates ExternalId
/// Validates: Requirements 7.3, 7.4
/// For a batch where some succeed and some fail, successful ones get ExternalId set,
/// failed ones retain null, and all categories in the batch are attempted.
/// </summary>
[Trait("Feature", "category-management")]
[Trait("Property", "9")]
public class PushOutcomePropertyTests
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
    /// For any batch of pending categories where the API succeeds for some and fails for others,
    /// each successfully pushed category SHALL have its ExternalId set to the server-returned value,
    /// each failed category SHALL retain ExternalId == null, and all categories in the batch
    /// SHALL be attempted regardless of individual failures.
    /// **Validates: Requirements 7.3, 7.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PushOutcome_SuccessfulGetsExternalId_FailedRetainsNull(
        byte batchSizeByte,
        uint successMaskRaw,
        PositiveInt baseServerId)
    {
        // Constrain batch size to 1..10 for reasonable test performance
        int batchSize = (batchSizeByte % 10) + 1;

        // Use a bitmask to decide which categories succeed vs fail
        // Ensure at least one success and one failure to test the mixed scenario
        uint successMask = successMaskRaw;
        bool hasSuccess = false;
        bool hasFailure = false;
        for (int i = 0; i < batchSize; i++)
        {
            if ((successMask & (1u << i)) != 0) hasSuccess = true;
            else hasFailure = true;
        }

        // If all succeed or all fail, flip one bit to ensure mixed scenario
        if (!hasFailure)
            successMask &= ~(1u << 0); // make first one fail
        if (!hasSuccess)
            successMask |= (1u << (batchSize - 1)); // make last one succeed

        var dbName = $"PushOutcome_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);
        var baseId = baseServerId.Get;

        // Generate unique CategoryIds for each category in the batch
        var categoryIds = Enumerable.Range(0, batchSize)
            .Select(_ => Guid.NewGuid())
            .ToList();

        // Seed all categories as pending push (CategoryId != Empty, ExternalId == null)
        using (var ctx = await factory.CreateDbContextAsync())
        {
            for (int i = 0; i < batchSize; i++)
            {
                ctx.Category.Add(new CategoryDTO
                {
                    CategoryId = categoryIds[i],
                    ExternalId = null,
                    Name = $"Category_{i}",
                    IsMainCategory = true,
                    Inactive = false,
                    UserId = 1,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    SystemDefault = false,
                });
            }
            await ctx.SaveChangesAsync();
        }

        // Setup mocks — track which categories were attempted
        var categoryApiRepo = Substitute.For<ICategoryApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();
        var attemptedCategoryIds = new List<Guid>();

        // Configure mock: succeed or throw based on the bitmask
        categoryApiRepo.PostCategoryAsync(Arg.Any<CategoryReq>())
            .Returns(callInfo =>
            {
                var req = callInfo.Arg<CategoryReq>();
                var catId = req.CategoryId!.Value;
                attemptedCategoryIds.Add(catId);

                int idx = categoryIds.IndexOf(catId);
                bool shouldSucceed = (successMask & (1u << idx)) != 0;

                if (shouldSucceed)
                    return new CategoryPushRes { Id = baseId + idx };
                else
                    throw new Exception("Simulated API failure");
            });

        var categoryRepo = new CategoryRepo(factory);
        var service = new CategoryService(categoryRepo, categoryApiRepo, syncCursorRepo);

        // Act
        await service.PushAsync();

        // Verify
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var allCategories = await ctx.Category.ToListAsync();

            for (int i = 0; i < batchSize; i++)
            {
                var cat = allCategories.First(c => c.CategoryId == categoryIds[i]);
                bool shouldSucceed = (successMask & (1u << i)) != 0;

                if (shouldSucceed)
                {
                    // Successful ones must have ExternalId set to server-returned value
                    if (cat.ExternalId != baseId + i)
                        return false;
                }
                else
                {
                    // Failed ones must retain ExternalId == null
                    if (cat.ExternalId != null)
                        return false;
                }
            }

            // All categories in the batch must have been attempted
            if (attemptedCategoryIds.Count != batchSize)
                return false;

            return true;
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
