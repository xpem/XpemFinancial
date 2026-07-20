using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Model.Resp.Api;
using NSubstitute;
using Repo;
using Xunit;

namespace RecurringTests.TransferTests;

/// <summary>
/// Unit tests for TransactionService transfer synchronization logic:
/// - Push defers when destination account has no ExternalId
/// - Pull stores without destination when account not found locally
/// - Pull resolves external DestinationAccountId to local Id
/// </summary>
[Trait("Feature", "transfer-transactions")]
public class TransferSyncUnitTests
{
    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private static AccountDTO MakeAccount(int id, int? externalId = null, decimal balance = 0m) => new()
    {
        Id = id,
        ExternalId = externalId,
        Name = $"Account {id}",
        CurrentBalance = balance,
        UserId = 1,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static TransactionDTO MakeTransfer(int originId, int destId, decimal amount) => new()
    {
        TransactionId = Guid.NewGuid(),
        UserId = 1,
        Description = "Test Transfer",
        Date = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc),
        Amount = amount,
        Type = TransactionType.Transfer,
        CategoryId = null,
        Repetition = Repetition.None,
        AccountId = originId,
        DestinationAccountId = destId,
        SyncStatus = TransactionSyncStatus.Pending,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    // ═══════════════════════════════════════════════════════════════════════
    // 1. PUSH — DESTINATION ACCOUNT WITHOUT EXTERNALID DEFERS PUSH
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// PushPendingAsync: when destination account has no ExternalId, the transfer
    /// is NOT sent to the server (PostAsync not called) and SyncStatus remains Pending.
    /// Validates: Requirements 7.1, 7.2
    /// </summary>
    [Fact]
    public async Task PushPending_DestAccountNoExternalId_DefersPush()
    {
        // Arrange: origin has ExternalId, destination does NOT
        var origin = MakeAccount(1, externalId: 100);
        var destination = MakeAccount(2, externalId: null);
        var ctx = await TransferGenerators.CreateServiceContext(origin, destination);

        // Create and persist a transfer with Pending status
        var transfer = MakeTransfer(origin.Id, destination.Id, amount: 50m);
        await ctx.Service.AddAsync(transfer, isOnline: false);

        // Act: trigger push cycle
        await ctx.Service.PushPendingAsync(userId: 1);

        // Assert: PostAsync was NOT called (push was deferred)
        await ctx.TransactionApiRepo.DidNotReceive().PostAsync(Arg.Any<Model.Req.TransactionReq>());

        // Assert: SyncStatus remains Pending
        using var db = await ctx.Factory.CreateDbContextAsync();
        var persisted = await db.Transaction.FirstAsync();
        Assert.Equal(TransactionSyncStatus.Pending, persisted.SyncStatus);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2. PULL — DESTINATION ACCOUNT NOT LOCAL → STORES WITHOUT DESTINATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// PullAsync: when the server returns a transfer with a DestinationAccountId that
    /// does not map to any local account, the transaction is stored with
    /// DestinationAccountId = null.
    /// Validates: Requirements 7.4
    /// </summary>
    [Fact]
    public async Task Pull_DestAccountNotLocal_StoresWithoutDestination()
    {
        // Arrange: setup in-memory DB with only the origin account
        var origin = MakeAccount(1, externalId: 500);
        var factory = await TransferGenerators.CreateSeededFactory(origin);

        var transactionApiRepo = Substitute.For<ApiRepo.ITransactionApiRepo>();
        var categoryRepo = Substitute.For<ICategoryRepo>();
        var accountRepo = Substitute.For<IAccountRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        // Configure accountRepo: origin resolves, destination does NOT (returns 0)
        int originExternalId = 500;
        int unknownDestExternalId = 999;
        accountRepo.GetLocalIdByExternalIdAsync(originExternalId).Returns(Task.FromResult(origin.Id));
        accountRepo.GetLocalIdByExternalIdAsync(unknownDestExternalId).Returns(Task.FromResult(0));

        // API returns a transfer pointing to an unknown destination
        var apiTransaction = new TransactionApiRes
        {
            Id = 42,
            TransactionId = Guid.NewGuid(),
            Description = "Server Transfer",
            Date = new DateTime(2024, 7, 1, 12, 0, 0, DateTimeKind.Utc),
            Amount = -75m,
            Type = (int)TransactionType.Transfer,
            AccountId = originExternalId,
            DestinationAccountId = unknownDestExternalId,
            Inactive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Repetition = (int)Repetition.None,
        };
        transactionApiRepo.GetByUpdatedAtAsync(Arg.Any<DateTime>(), Arg.Any<int>())
            .Returns(Task.FromResult<List<TransactionApiRes>?>(new List<TransactionApiRes> { apiTransaction }));

        var transactionRepo = new TransactionRepo(factory);
        var service = new Service.Transaction.TransactionService(
            transactionRepo, transactionApiRepo, categoryRepo, accountRepo, syncCursorRepo);

        // Act: pull from server
        await service.PullAsync(uid: 1, lastUpdatedAt: DateTime.MinValue);

        // Assert: transaction was stored with DestinationAccountId = null
        using var db = await factory.CreateDbContextAsync();
        var persisted = await db.Transaction.FirstAsync();
        Assert.Null(persisted.DestinationAccountId);
        Assert.Equal(TransactionType.Transfer, persisted.Type);
        Assert.Equal(-75m, persisted.Amount);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3. PULL — DESTINATION ACCOUNT EXISTS → RESOLVES TO LOCAL ID
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// PullAsync: when the server returns a transfer with a DestinationAccountId that
    /// maps to a local account, the transaction is stored with the correct local
    /// DestinationAccountId.
    /// Validates: Requirements 7.3
    /// </summary>
    [Fact]
    public async Task Pull_DestAccountExists_ResolvesToLocalId()
    {
        // Arrange: setup in-memory DB with origin and destination accounts
        var origin = MakeAccount(1, externalId: 500);
        var destination = MakeAccount(2, externalId: 600);
        var factory = await TransferGenerators.CreateSeededFactory(origin, destination);

        var transactionApiRepo = Substitute.For<ApiRepo.ITransactionApiRepo>();
        var categoryRepo = Substitute.For<ICategoryRepo>();
        var accountRepo = Substitute.For<IAccountRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        // Configure accountRepo: both resolve to their local IDs
        int originExternalId = 500;
        int destExternalId = 600;
        accountRepo.GetLocalIdByExternalIdAsync(originExternalId).Returns(Task.FromResult(origin.Id));
        accountRepo.GetLocalIdByExternalIdAsync(destExternalId).Returns(Task.FromResult(destination.Id));

        // API returns a transfer with known destination
        var apiTransaction = new TransactionApiRes
        {
            Id = 55,
            TransactionId = Guid.NewGuid(),
            Description = "Server Transfer Resolved",
            Date = new DateTime(2024, 7, 10, 12, 0, 0, DateTimeKind.Utc),
            Amount = -120m,
            Type = (int)TransactionType.Transfer,
            AccountId = originExternalId,
            DestinationAccountId = destExternalId,
            Inactive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Repetition = (int)Repetition.None,
        };
        transactionApiRepo.GetByUpdatedAtAsync(Arg.Any<DateTime>(), Arg.Any<int>())
            .Returns(Task.FromResult<List<TransactionApiRes>?>(new List<TransactionApiRes> { apiTransaction }));

        var transactionRepo = new TransactionRepo(factory);
        var service = new Service.Transaction.TransactionService(
            transactionRepo, transactionApiRepo, categoryRepo, accountRepo, syncCursorRepo);

        // Act: pull from server
        await service.PullAsync(uid: 1, lastUpdatedAt: DateTime.MinValue);

        // Assert: transaction was stored with local DestinationAccountId resolved
        using var db = await factory.CreateDbContextAsync();
        var persisted = await db.Transaction.FirstAsync();
        Assert.Equal(destination.Id, persisted.DestinationAccountId);
        Assert.Equal(TransactionType.Transfer, persisted.Type);
        Assert.Equal(-120m, persisted.Amount);
    }
}
