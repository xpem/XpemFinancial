using Microsoft.EntityFrameworkCore;
using Model.DTO;
using NSubstitute;
using Repo;
using Xunit;

namespace RecurringTests.TransferTests;

/// <summary>
/// Unit tests for TransactionService transfer-specific logic:
/// creation (happy path + validation), editing with account changes, and deletion balance revert.
/// Uses the same in-memory DB + NSubstitute pattern as TransferGenerators.
/// </summary>
[Trait("Feature", "transfer-transactions")]
public class TransferServiceUnitTests
{
    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private static AccountDTO MakeAccount(int id, string name = "Account", decimal balance = 0m) => new()
    {
        Id = id,
        Name = $"{name} {id}",
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
    // 1. CREATION — HAPPY PATH
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// AddAsync with a positive amount creates a persisted record with negative Amount.
    /// Requirements: 1.3
    /// </summary>
    [Fact]
    public async Task AddAsync_Transfer_ValidFields_PersistsWithNegativeAmount()
    {
        // Arrange
        var origin = MakeAccount(1);
        var destination = MakeAccount(2);
        var ctx = await TransferGenerators.CreateServiceContext(origin, destination);

        var transfer = MakeTransfer(origin.Id, destination.Id, amount: 150.00m);

        // Act
        await ctx.Service.AddAsync(transfer, isOnline: false);

        // Assert
        using var db = await ctx.Factory.CreateDbContextAsync();
        var persisted = await db.Transaction.FirstAsync();

        Assert.Equal(-150.00m, persisted.Amount);
        Assert.Null(persisted.CategoryId);
        Assert.Equal(Repetition.None, persisted.Repetition);
        Assert.Equal(TransactionType.Transfer, persisted.Type);
        Assert.Equal(destination.Id, persisted.DestinationAccountId);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2. VALIDATION — NULL DESTINATION ACCOUNT
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// AddAsync with DestinationAccountId = null throws ArgumentException.
    /// Requirements: 1.4
    /// </summary>
    [Fact]
    public async Task AddAsync_Transfer_NullDestinationAccountId_ThrowsArgException()
    {
        // Arrange
        var origin = MakeAccount(1);
        var destination = MakeAccount(2);
        var ctx = await TransferGenerators.CreateServiceContext(origin, destination);

        var transfer = MakeTransfer(origin.Id, destination.Id, amount: 100m);
        transfer.DestinationAccountId = null;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => ctx.Service.AddAsync(transfer, isOnline: false));

        Assert.Contains("DestinationAccountId", ex.Message);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3. VALIDATION — SAME ORIGIN AND DESTINATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// AddAsync with DestinationAccountId == AccountId throws ArgumentException.
    /// Requirements: 1.4
    /// </summary>
    [Fact]
    public async Task AddAsync_Transfer_SameOriginAndDest_ThrowsArgException()
    {
        // Arrange
        var origin = MakeAccount(1);
        var destination = MakeAccount(2);
        var ctx = await TransferGenerators.CreateServiceContext(origin, destination);

        var transfer = MakeTransfer(origin.Id, origin.Id, amount: 50m); // same account

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => ctx.Service.AddAsync(transfer, isOnline: false));

        Assert.Contains("different", ex.Message);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4. VALIDATION — ZERO AMOUNT
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// AddAsync with Amount = 0 throws ArgumentException because -Math.Abs(0) == 0.
    /// Requirements: 1.3
    /// </summary>
    [Fact]
    public async Task AddAsync_Transfer_ZeroAmount_ThrowsArgException()
    {
        // Arrange
        var origin = MakeAccount(1);
        var destination = MakeAccount(2);
        var ctx = await TransferGenerators.CreateServiceContext(origin, destination);

        var transfer = MakeTransfer(origin.Id, destination.Id, amount: 0m);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => ctx.Service.AddAsync(transfer, isOnline: false));

        Assert.Contains("zero", ex.Message);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5. EDIT — CHANGE DESTINATION ACCOUNT RECALCULATES BOTH OLD AND NEW
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// UpdateAsync when DestinationAccountId changes: old destination balance reverts,
    /// new destination balance receives the transfer amount.
    /// Requirements: 3.1, 3.2
    /// </summary>
    [Fact]
    public async Task UpdateAsync_Transfer_ChangeDestAccount_RecalculatesBothOldAndNew()
    {
        // Arrange: 3 accounts — origin, old destination, new destination
        var origin = MakeAccount(1);
        var oldDest = MakeAccount(2);
        var newDest = MakeAccount(3);

        // Seed all three accounts into the in-memory database
        var factory = await TransferGenerators.CreateSeededFactory(origin, oldDest, newDest);

        var transactionApiRepo = NSubstitute.Substitute.For<ApiRepo.ITransactionApiRepo>();
        var categoryRepo = NSubstitute.Substitute.For<ICategoryRepo>();
        var accountRepo = NSubstitute.Substitute.For<IAccountRepo>();
        var syncCursorRepo = NSubstitute.Substitute.For<ISyncCursorRepo>();

        accountRepo.GetByIdAsync(origin.Id).Returns(origin);
        accountRepo.GetByIdAsync(oldDest.Id).Returns(oldDest);
        accountRepo.GetByIdAsync(newDest.Id).Returns(newDest);

        var transactionRepo = new TransactionRepo(factory);
        var service = new Service.Transaction.TransactionService(
            transactionRepo, transactionApiRepo, categoryRepo, accountRepo, syncCursorRepo);

        var transfer = MakeTransfer(origin.Id, oldDest.Id, amount: 200m);

        // Act: Create transfer (origin→oldDest)
        await service.AddAsync(transfer, isOnline: false);

        // Verify initial state
        Assert.Equal(-200m, origin.CurrentBalance);
        Assert.Equal(200m, oldDest.CurrentBalance);
        Assert.Equal(0m, newDest.CurrentBalance);

        // Load persisted record and change destination
        var persisted = await service.GetByIdAsync(transfer.Id);
        persisted.DestinationAccountId = newDest.Id;
        persisted.DestinationAccount = null; // clear navigation to avoid EF tracking conflicts
        persisted.Amount = 200m; // positive input; service negates

        // Act: Update (change destination to newDest)
        await service.UpdateAsync(persisted, isOnline: false);

        // Assert: oldDest balance reverted to 0, newDest now has +200
        Assert.Equal(-200m, origin.CurrentBalance);
        Assert.Equal(0m, oldDest.CurrentBalance);
        Assert.Equal(200m, newDest.CurrentBalance);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 6. DELETE — REVERTS DESTINATION BALANCE
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// DeleteAsync soft-deletes the transfer, so GetSumByAccountIdAsync excludes it,
    /// reverting the destination account balance to 0.
    /// Requirements: 3.3
    /// </summary>
    [Fact]
    public async Task DeleteAsync_Transfer_RevertsDestinationBalance()
    {
        // Arrange
        var origin = MakeAccount(1);
        var destination = MakeAccount(2);
        var ctx = await TransferGenerators.CreateServiceContext(origin, destination);

        var transfer = MakeTransfer(origin.Id, destination.Id, amount: 300m);

        // Create the transfer
        await ctx.Service.AddAsync(transfer, isOnline: false);

        // Verify balances after creation
        Assert.Equal(-300m, origin.CurrentBalance);
        Assert.Equal(300m, destination.CurrentBalance);

        // Get persisted ID for deletion
        int transactionId;
        using (var db = await ctx.Factory.CreateDbContextAsync())
        {
            var persisted = await db.Transaction.FirstAsync();
            transactionId = persisted.Id;
        }

        // Act: Delete the transfer
        await ctx.Service.DeleteAsync(transactionId, isOnline: false);

        // Assert: destination balance reverts to 0 (inactive transaction excluded from sum)
        Assert.Equal(0m, destination.CurrentBalance);
        Assert.Equal(0m, origin.CurrentBalance);
    }
}
