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
/// Property 9: Atomic ExternalId Persist After Push
/// Validates: Requirements 7.1, 7.3
/// </summary>
[Trait("Feature", "transaction-guid-sync")]
[Trait("Property", "9")]
public class AtomicExternalIdPropertyTests
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
    /// For any recurring occurrence with SyncStatus=Pending and a valid TransactionId,
    /// when PushPendingAsync is called and the server responds with a valid ExternalId (> 0),
    /// the local record SHALL have both ExternalId == serverId AND SyncStatus == Synced
    /// set atomically after the operation completes.
    /// **Validates: Requirements 7.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> ExternalIdAndSyncedAreSetTogether(Guid transactionId, int userId, int serverId)
    {
        if (transactionId == Guid.Empty) return true; // skip trivial case
        if (userId <= 0) return true; // skip invalid userId
        if (serverId <= 0) return true; // skip invalid serverId

        var dbName = $"AtomicExternalId_SetTogether_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var accountId = 1;
        var accountExternalId = 500;
        var recurringRuleId = Guid.NewGuid();

        // Seed a recurring occurrence with SyncStatus=Pending, TransactionId set, no ExternalId
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
                Description = "Recurring occurrence",
                Date = DateTime.UtcNow,
                Amount = 100m,
                AccountId = accountId,
                SyncStatus = TransactionSyncStatus.Pending,
                RecurringRuleId = recurringRuleId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                // ExternalId is null (not set)
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

        // PostAsync returns the serverId (simulating successful push)
        transactionApiRepo.PostAsync(Arg.Any<TransactionReq>()).Returns(serverId);

        var transactionRepo = new TransactionRepo(factory);
        var service = new TransactionService(
            transactionRepo, transactionApiRepo, categoryRepo, accountRepo, syncCursorRepo);

        await service.PushPendingAsync(userId);

        // Verify: ExternalId == serverId AND SyncStatus == Synced (both set atomically)
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
    /// For any recurring occurrence successfully pushed (server returned ExternalId > 0),
    /// GetPendingPushAsync SHALL NOT return this record on subsequent calls.
    /// **Validates: Requirements 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PushedRecordExcludedFromGetPendingPush(Guid transactionId, int userId, int serverId)
    {
        if (transactionId == Guid.Empty) return true; // skip trivial case
        if (userId <= 0) return true; // skip invalid userId
        if (serverId <= 0) return true; // skip invalid serverId

        var dbName = $"AtomicExternalId_Excluded_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var accountId = 1;
        var accountExternalId = 600;
        var recurringRuleId = Guid.NewGuid();

        // Seed a recurring occurrence with SyncStatus=Pending, TransactionId set, no ExternalId
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
                Description = "Recurring occurrence pending",
                Date = DateTime.UtcNow,
                Amount = 200m,
                AccountId = accountId,
                SyncStatus = TransactionSyncStatus.Pending,
                RecurringRuleId = recurringRuleId,
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

        // PostAsync returns a valid serverId
        transactionApiRepo.PostAsync(Arg.Any<TransactionReq>()).Returns(serverId);

        var transactionRepo = new TransactionRepo(factory);
        var service = new TransactionService(
            transactionRepo, transactionApiRepo, categoryRepo, accountRepo, syncCursorRepo);

        await service.PushPendingAsync(userId);

        // Verify: GetPendingPushAsync returns empty list (record excluded after successful push)
        var pending = await transactionRepo.GetPendingPushAsync(userId);
        return pending.Count == 0;
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
