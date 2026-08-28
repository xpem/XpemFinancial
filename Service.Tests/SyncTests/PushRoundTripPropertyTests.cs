using ApiRepo;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Model.Req;
using NSubstitute;
using Repo;
using Service.Transaction;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Property 3: Push Round-Trip Preserves Identity
/// Validates: Requirements 3.1, 3.2
/// </summary>
[Trait("Feature", "transaction-guid-sync")]
[Trait("Property", "3")]
public class PushRoundTripPropertyTests
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
    /// For any transaction with a non-empty TransactionId and SyncStatus=Pending,
    /// when PushPendingAsync is called, the TransactionReq sent to PostAsync
    /// SHALL contain the local TransactionId.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PushIncludesTransactionIdInRequest(Guid transactionId, int userId)
    {
        if (transactionId == Guid.Empty) return true; // skip trivial case
        if (userId <= 0) return true; // skip invalid userId

        var dbName = $"PushRoundTrip_IncludesId_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed a transaction with SyncStatus=Pending and a valid TransactionId
        var accountId = 1;
        var accountExternalId = 100;

        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Account.Add(new AccountDTO
            {
                Id = accountId,
                Name = "Test Account",
                ExternalId = accountExternalId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });

            ctx.Transaction.Add(new TransactionDTO
            {
                TransactionId = transactionId,
                UserId = userId,
                Description = "Test push",
                Date = DateTime.UtcNow,
                Amount = 50m,
                AccountId = accountId,
                SyncStatus = TransactionSyncStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });

            await ctx.SaveChangesAsync();
        }

        // Setup mocks
        var transactionApiRepo = Substitute.For<ITransactionApiRepo>();
        var categoryRepo = Substitute.For<ICategoryRepo>();
        var accountRepo = Substitute.For<IAccountRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        // AccountRepo returns an account with ExternalId so push can proceed
        accountRepo.GetByIdAsync(accountId).Returns(new AccountDTO
        {
            Id = accountId,
            Name = "Test Account",
            ExternalId = accountExternalId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        // Capture the request sent to PostAsync and return a valid serverId
        TransactionReq? capturedReq = null;
        transactionApiRepo.PostAsync(Arg.Do<TransactionReq>(r => capturedReq = r))
            .Returns(42);

        var transactionRepo = new TransactionRepo(factory);
        var service = new TransactionService(
            transactionRepo, transactionApiRepo, categoryRepo, accountRepo, syncCursorRepo);

        await service.PushPendingAsync(userId);

        // Verify: the captured request contains the correct TransactionId
        return capturedReq is not null && capturedReq.TransactionId == transactionId;
    }

    /// <summary>
    /// For any transaction with a non-empty TransactionId, when the server responds
    /// with a valid ExternalId (> 0), the local record SHALL have that ExternalId persisted
    /// and SyncStatus set to Synced.
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> SuccessfulPushPersistsExternalId(Guid transactionId, int userId, int serverId)
    {
        if (transactionId == Guid.Empty) return true; // skip trivial case
        if (userId <= 0) return true; // skip invalid userId
        if (serverId <= 0) return true; // skip invalid serverId (must be > 0 for success)

        var dbName = $"PushRoundTrip_ExternalId_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var accountId = 1;
        var accountExternalId = 200;

        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Account.Add(new AccountDTO
            {
                Id = accountId,
                Name = "Test Account",
                ExternalId = accountExternalId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });

            ctx.Transaction.Add(new TransactionDTO
            {
                TransactionId = transactionId,
                UserId = userId,
                Description = "Test push external",
                Date = DateTime.UtcNow,
                Amount = 75m,
                AccountId = accountId,
                SyncStatus = TransactionSyncStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });

            await ctx.SaveChangesAsync();
        }

        // Setup mocks
        var transactionApiRepo = Substitute.For<ITransactionApiRepo>();
        var categoryRepo = Substitute.For<ICategoryRepo>();
        var accountRepo = Substitute.For<IAccountRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        accountRepo.GetByIdAsync(accountId).Returns(new AccountDTO
        {
            Id = accountId,
            Name = "Test Account",
            ExternalId = accountExternalId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        // PostAsync returns the serverId
        transactionApiRepo.PostAsync(Arg.Any<TransactionReq>()).Returns(serverId);

        var transactionRepo = new TransactionRepo(factory);
        var service = new TransactionService(
            transactionRepo, transactionApiRepo, categoryRepo, accountRepo, syncCursorRepo);

        await service.PushPendingAsync(userId);

        // Verify: the local record now has ExternalId = serverId and SyncStatus = Synced
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var record = await ctx.Transaction
                .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

            return record is not null
                && record.ExternalId == serverId
                && record.SyncStatus == TransactionSyncStatus.Synced;
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
