using ApiRepo;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using NSubstitute;
using Repo;
using Service.Category;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Property 1: Guid Assignment on Creation
/// Validates: Requirements 1.2, 10.1, 10.2
/// </summary>
[Trait("Feature", "category-guid-sync")]
[Trait("Property", "1")]
public class CategoryGuidAssignmentPropertyTests
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
    /// For any category created via AddLocalAsync with CategoryId == Guid.Empty,
    /// the persisted record SHALL have a CategoryId != Guid.Empty.
    /// **Validates: Requirements 1.2, 10.1, 10.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> AddLocalAsync_WithEmptyGuid_AssignsNonEmptyGuid(
        NonEmptyString name, int userId)
    {
        var dbName = $"CategoryGuidAssign_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);
        var categoryRepo = new CategoryRepo(factory);

        var categoryApiRepo = Substitute.For<ICategoryApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        var service = new CategoryService(categoryRepo, categoryApiRepo, syncCursorRepo);

        var category = new CategoryDTO
        {
            CategoryId = Guid.Empty,
            Name = name.Get,
            UserId = userId,
            IsMainCategory = true,
        };

        await service.AddLocalAsync(category);

        // Query the database to verify the stored record
        using var ctx = await factory.CreateDbContextAsync();
        var stored = await ctx.Category.FirstOrDefaultAsync();

        return stored is not null && stored.CategoryId != Guid.Empty;
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
