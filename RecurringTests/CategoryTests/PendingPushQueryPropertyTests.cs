// Feature: category-management, Property 8: Pending push query correctness
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using NSubstitute;
using Repo;
using Service.Category;
using Xunit;

namespace RecurringTests.CategoryTests;

/// <summary>
/// Property 8: Pending push query correctness
/// Validates: Requirements 7.2
/// For any set of categories with varying CategoryId and ExternalId values,
/// the pending push query returns exactly those where CategoryId != Guid.Empty AND ExternalId == null.
/// </summary>
[Trait("Feature", "category-management")]
[Trait("Property", "8")]
public class PendingPushQueryPropertyTests
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
    /// For any set of categories with varying CategoryId and ExternalId values,
    /// GetPendingPushAsync returns exactly those where CategoryId != Guid.Empty AND ExternalId == null.
    /// **Validates: Requirements 7.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PendingPush_ReturnsExactly_CategoriesWithNonEmptyCategoryIdAndNullExternalId(
        byte categoryCount,
        int seed)
    {
        // Limit category count to 0..30
        int n = categoryCount % 31;
        var dbName = $"PendingPush_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);
        var rng = new Random(seed);

        var seededCategories = new List<CategoryDTO>();

        // Seed categories with random CategoryId and ExternalId combinations
        using (var ctx = await factory.CreateDbContextAsync())
        {
            for (int i = 0; i < n; i++)
            {
                // Randomly assign CategoryId: either Guid.Empty or a real Guid
                var categoryId = rng.Next(2) == 0 ? Guid.Empty : Guid.NewGuid();

                // Randomly assign ExternalId: either null or a positive int
                int? externalId = rng.Next(2) == 0 ? null : rng.Next(1, 10000);

                var category = new CategoryDTO
                {
                    CategoryId = categoryId,
                    ExternalId = externalId,
                    Name = $"Cat_{i}",
                    IsMainCategory = rng.Next(2) == 0,
                    Inactive = rng.Next(2) == 0,
                    UserId = 1,
                    SystemDefault = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };

                ctx.Category.Add(category);
                seededCategories.Add(category);
            }

            await ctx.SaveChangesAsync();
        }

        // Compute expected result using the same predicate as GetPendingPushAsync
        var expected = seededCategories
            .Where(c => c.CategoryId != Guid.Empty && c.ExternalId == null)
            .ToList();

        // Use the real repo method
        var categoryRepo = new CategoryRepo(factory);
        var actual = await categoryRepo.GetPendingPushAsync();

        // Verify: counts match and every expected item is present
        if (actual.Count != expected.Count)
            return false;

        // Verify by CategoryId match (CategoryId is unique per non-empty entry)
        var actualCategoryIds = actual.Select(c => c.CategoryId).ToHashSet();
        foreach (var exp in expected)
        {
            if (!actualCategoryIds.Contains(exp.CategoryId))
                return false;
        }

        // Also verify no unexpected items: every actual result satisfies the predicate
        foreach (var item in actual)
        {
            if (item.CategoryId == Guid.Empty || item.ExternalId != null)
                return false;
        }

        return true;
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
