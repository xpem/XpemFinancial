using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Repo;
using Xunit;

namespace RecurringTests.TransferTests;

/// <summary>
/// Property 1: Transfer field invariants
/// For any transfer creation with a non-zero amount and two distinct accounts,
/// the persisted record SHALL have Amount &lt; 0, CategoryId == null,
/// DestinationAccountId != null, and DestinationAccountId != AccountId.
/// **Validates: Requirements 1.3, 1.4, 1.5**
/// </summary>
[Trait("Feature", "transfer-transactions")]
[Trait("Property", "1")]
public class TransferFieldInvariantsPropertyTests
{
    /// <summary>
    /// For any positive amount and two distinct accounts, when AddAsync is called
    /// with Type=Transfer, the persisted record has Amount &lt; 0, CategoryId == null,
    /// DestinationAccountId != null, and DestinationAccountId != AccountId.
    /// **Validates: Requirements 1.3, 1.4, 1.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> TransferCreation_EnforcesFieldInvariants(
        int originId, int destOffset, int amountInt, int originBalance, int destBalance)
    {
        // Constrain inputs to valid ranges (same logic as TransferGenerators)
        if (originId < 1 || originId > 500) return true;
        if (destOffset < 1 || destOffset > 499) return true;
        if (amountInt < 1 || amountInt > 100_000) return true;
        if (originBalance < -100_000 || originBalance > 100_000) return true;
        if (destBalance < -100_000 || destBalance > 100_000) return true;

        int destId = ((originId + destOffset - 1) % 1000) + 1;
        decimal positiveAmount = amountInt / 100m;

        // Build accounts (same as TransferGenerators.AccountPair)
        var origin = new AccountDTO
        {
            Id = originId,
            Name = $"Account {originId}",
            CurrentBalance = originBalance / 100m,
            UserId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var destination = new AccountDTO
        {
            Id = destId,
            Name = $"Account {destId}",
            CurrentBalance = destBalance / 100m,
            UserId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        // Arrange
        var ctx = await TransferGenerators.CreateServiceContext(origin, destination);

        var transactionId = Guid.NewGuid();
        var transaction = new TransactionDTO
        {
            TransactionId = transactionId,
            UserId = 1,
            Description = "Transfer test",
            Date = DateTime.UtcNow,
            Amount = positiveAmount, // positive input — service should negate
            Type = TransactionType.Transfer,
            CategoryId = 999, // intentionally non-null to verify service clears it
            Repetition = Repetition.None,
            AccountId = origin.Id,
            DestinationAccountId = destination.Id,
            SyncStatus = TransactionSyncStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        // Act
        await ctx.Service.AddAsync(transaction, isOnline: false);

        // Assert — read persisted record
        using var dbCtx = await ctx.Factory.CreateDbContextAsync();
        var persisted = await dbCtx.Transaction
            .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

        if (persisted is null) return false;

        bool amountIsNegative = persisted.Amount < 0;
        bool categoryIsNull = persisted.CategoryId == null;
        bool destinationIsNotNull = persisted.DestinationAccountId != null;
        bool destinationDiffersFromOrigin = persisted.DestinationAccountId != persisted.AccountId;

        return amountIsNegative
            && categoryIsNull
            && destinationIsNotNull
            && destinationDiffersFromOrigin;
    }
}
