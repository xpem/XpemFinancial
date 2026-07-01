using ApiRepo;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using NSubstitute;
using Repo;
using Service.Transaction;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Property 1: Guid Assignment on Creation
/// Validates: Requirements 1.2, 2.5
/// </summary>
[Trait("Feature", "transaction-guid-sync")]
[Trait("Property", "1")]
public class GuidAssignmentPropertyTests
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
    /// For any transaction created via AddAsync with TransactionId == Guid.Empty,
    /// the persisted record SHALL have a TransactionId != Guid.Empty.
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> AddAsync_WithEmptyGuid_AssignsNonEmptyGuid(
        NonEmptyString description, int userId, int accountId)
    {
        var dbName = $"GuidAssign_Add_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);
        var transactionRepo = new TransactionRepo(factory);

        var transactionApiRepo = Substitute.For<ITransactionApiRepo>();
        var categoryRepo = Substitute.For<ICategoryRepo>();
        var accountRepo = Substitute.For<IAccountRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        var service = new TransactionService(
            transactionRepo, transactionApiRepo, categoryRepo, accountRepo, syncCursorRepo);

        var transaction = new TransactionDTO
        {
            TransactionId = Guid.Empty,
            Description = description.Get,
            UserId = userId,
            AccountId = accountId,
            Date = DateTime.UtcNow,
            Amount = 100m,
            Repetition = Repetition.None,
        };

        await service.AddAsync(transaction, isOnline: false);

        // Query the database to verify the stored record
        using var ctx = await factory.CreateDbContextAsync();
        var stored = await ctx.Transaction.FirstOrDefaultAsync();

        return stored is not null && stored.TransactionId != Guid.Empty;
    }

    /// <summary>
    /// For any transaction created via AddOccurrenceAsync with TransactionId == Guid.Empty,
    /// the persisted record SHALL have a TransactionId != Guid.Empty.
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> AddOccurrenceAsync_WithEmptyGuid_AssignsNonEmptyGuid(
        NonEmptyString description, int userId, int accountId)
    {
        var dbName = $"GuidAssign_Occurrence_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);
        var transactionRepo = new TransactionRepo(factory);

        var transactionApiRepo = Substitute.For<ITransactionApiRepo>();
        var categoryRepo = Substitute.For<ICategoryRepo>();
        var accountRepo = Substitute.For<IAccountRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        var service = new TransactionService(
            transactionRepo, transactionApiRepo, categoryRepo, accountRepo, syncCursorRepo);

        var occurrence = new TransactionDTO
        {
            TransactionId = Guid.Empty,
            Description = description.Get,
            UserId = userId,
            AccountId = accountId,
            Date = DateTime.UtcNow,
            Amount = 50m,
            Repetition = Repetition.Recurring,
        };

        await service.AddOccurrenceAsync(occurrence);

        // Query the database to verify the stored record
        using var ctx = await factory.CreateDbContextAsync();
        var stored = await ctx.Transaction.FirstOrDefaultAsync();

        return stored is not null && stored.TransactionId != Guid.Empty;
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
