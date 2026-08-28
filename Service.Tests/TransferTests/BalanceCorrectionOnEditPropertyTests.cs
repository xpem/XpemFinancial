using FsCheck.Xunit;
using Model.DTO;
using Xunit;

namespace RecurringTests.TransferTests;

/// <summary>
/// Property 5: Balance correction on transfer value edit
/// For any transfer with original value V_old edited to V_new (both > 0),
/// the origin account balance change SHALL be (V_old - V_new) and the
/// destination account balance change SHALL be (V_new - V_old).
/// **Validates: Requirements 3.1, 3.2**
/// </summary>
[Trait("Feature", "transfer-transactions")]
[Trait("Property", "5")]
public class BalanceCorrectionOnEditPropertyTests
{
    /// <summary>
    /// For any two distinct positive amounts V_old and V_new, when a transfer is created
    /// with V_old and then edited to V_new, the origin balance change = (V_old - V_new)
    /// and the destination balance change = (V_new - V_old).
    /// **Validates: Requirements 3.1, 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> EditingTransferValue_CorrectlyAdjustsBalances(
        int amountOldInt, int amountNewInt, int originId, int destOffset)
    {
        // Constrain inputs to valid ranges
        if (amountOldInt <= 0 || amountOldInt > 100_000) return true;
        if (amountNewInt <= 0 || amountNewInt > 100_000) return true;
        if (originId <= 0 || originId > 500) return true;
        if (destOffset <= 0 || destOffset > 499) return true;

        decimal vOld = amountOldInt / 100m;
        decimal vNew = amountNewInt / 100m;

        // Skip trivial case where amounts are equal (no actual edit)
        if (vOld == vNew) return true;

        int destId = ((originId + destOffset - 1) % 1000) + 1;

        // Ensure accounts are distinct
        if (originId == destId) return true;

        var origin = new AccountDTO
        {
            Id = originId,
            Name = $"Account {originId}",
            CurrentBalance = 0m,
            UserId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var destination = new AccountDTO
        {
            Id = destId,
            Name = $"Account {destId}",
            CurrentBalance = 0m,
            UserId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        // Arrange
        var ctx = await TransferGenerators.CreateServiceContext(origin, destination);

        var transaction = new TransactionDTO
        {
            TransactionId = Guid.NewGuid(),
            UserId = origin.UserId,
            Description = "Transfer edit test",
            Date = DateTime.UtcNow,
            Amount = vOld, // positive input; service negates it
            Type = TransactionType.Transfer,
            CategoryId = null,
            Repetition = Repetition.None,
            AccountId = origin.Id,
            DestinationAccountId = destination.Id,
            SyncStatus = TransactionSyncStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        // Step 1: Create the transfer with V_old
        await ctx.Service.AddAsync(transaction, isOnline: false);

        // Step 2: Record balances after creation.
        // RecalculateAccountBalanceAsync mutates the object reference returned by accountRepo mock.
        decimal originBalanceAfterCreate = origin.CurrentBalance;
        decimal destBalanceAfterCreate = destination.CurrentBalance;

        // Step 3: Load the persisted transaction and change Amount to V_new
        using var db = await ctx.Factory.CreateDbContextAsync();
        var persisted = await db.Transaction.FindAsync(transaction.Id);
        if (persisted is null) return false;

        var toUpdate = await ctx.Service.GetByIdAsync(persisted.Id);
        toUpdate.Amount = vNew; // positive value; UpdateAsync negates it for Transfer type

        // Step 4: Call UpdateAsync
        await ctx.Service.UpdateAsync(toUpdate, isOnline: false);

        // Step 5: Verify balance changes.
        // After edit, RecalculateAccountBalanceAsync recalculates from scratch:
        // origin.CurrentBalance = -V_new, destination.CurrentBalance = +V_new
        decimal originBalanceAfterEdit = origin.CurrentBalance;
        decimal destBalanceAfterEdit = destination.CurrentBalance;

        // Delta origin = (-V_new) - (-V_old) = V_old - V_new
        // Delta dest   = (+V_new) - (+V_old) = V_new - V_old
        decimal originDelta = originBalanceAfterEdit - originBalanceAfterCreate;
        decimal destDelta = destBalanceAfterEdit - destBalanceAfterCreate;

        decimal expectedOriginDelta = vOld - vNew;
        decimal expectedDestDelta = vNew - vOld;

        return originDelta == expectedOriginDelta && destDelta == expectedDestDelta;
    }
}
