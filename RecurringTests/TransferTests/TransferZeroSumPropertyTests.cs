using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Repo;
using Xunit;

namespace RecurringTests.TransferTests;

/// <summary>
/// Property 4: Transfer is zero-sum (patrimony invariant)
/// For any transfer operation (creation or deletion), the sum of all account balances
/// remains unchanged.
/// **Validates: Requirements 4.3**
/// </summary>
[Trait("Feature", "transfer-transactions")]
[Trait("Property", "4")]
public class TransferZeroSumPropertyTests
{
    /// <summary>
    /// For any transfer creation: the sum of all account balances before == sum after.
    /// Since balances are recalculated from transaction sums and no transactions exist initially,
    /// sum before = 0. After creating a transfer, origin gets -V and destination gets +V, so sum = 0.
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CreatingTransfer_PreservesTotalBalance()
    {
        return Prop.ForAll(
            TransferGenerators.AccountPair().ToArbitrary(),
            TransferGenerators.PositiveAmount().ToArbitrary(),
            (accounts, positiveAmount) =>
            {
                return CreatingTransfer_PreservesTotalBalance_Impl(
                    accounts.Origin, accounts.Destination, positiveAmount).Result;
            });
    }

    private static async Task<bool> CreatingTransfer_PreservesTotalBalance_Impl(
        AccountDTO origin, AccountDTO destination, decimal positiveAmount)
    {
        var ctx = await TransferGenerators.CreateServiceContext(origin, destination);

        // Sum of all account balances before the operation
        decimal sumBefore;
        using (var db = await ctx.Factory.CreateDbContextAsync())
        {
            var accounts = await db.Account.ToListAsync();
            sumBefore = accounts.Sum(a => a.CurrentBalance);
        }

        // Create the transfer
        var transaction = new TransactionDTO
        {
            TransactionId = Guid.NewGuid(),
            UserId = origin.UserId,
            Description = "Zero-sum test",
            Date = DateTime.UtcNow,
            Amount = positiveAmount,
            Type = TransactionType.Transfer,
            CategoryId = null,
            Repetition = Repetition.None,
            AccountId = origin.Id,
            DestinationAccountId = destination.Id,
            SyncStatus = TransactionSyncStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await ctx.Service.AddAsync(transaction, isOnline: false);

        // Sum of all account balances after the operation
        decimal sumAfter;
        using (var db = await ctx.Factory.CreateDbContextAsync())
        {
            var accounts = await db.Account.ToListAsync();
            sumAfter = accounts.Sum(a => a.CurrentBalance);
        }

        return sumBefore == sumAfter;
    }

    /// <summary>
    /// For any transfer deletion (soft-delete): the sum of all account balances before == sum after.
    /// After creating a transfer, we record the sum. Then we delete (soft-delete) the transfer
    /// and verify the sum remains the same.
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DeletingTransfer_PreservesTotalBalance()
    {
        return Prop.ForAll(
            TransferGenerators.AccountPair().ToArbitrary(),
            TransferGenerators.PositiveAmount().ToArbitrary(),
            (accounts, positiveAmount) =>
            {
                return DeletingTransfer_PreservesTotalBalance_Impl(
                    accounts.Origin, accounts.Destination, positiveAmount).Result;
            });
    }

    private static async Task<bool> DeletingTransfer_PreservesTotalBalance_Impl(
        AccountDTO origin, AccountDTO destination, decimal positiveAmount)
    {
        var ctx = await TransferGenerators.CreateServiceContext(origin, destination);

        // First create the transfer so we have something to delete
        var transaction = new TransactionDTO
        {
            TransactionId = Guid.NewGuid(),
            UserId = origin.UserId,
            Description = "Zero-sum delete test",
            Date = DateTime.UtcNow,
            Amount = positiveAmount,
            Type = TransactionType.Transfer,
            CategoryId = null,
            Repetition = Repetition.None,
            AccountId = origin.Id,
            DestinationAccountId = destination.Id,
            SyncStatus = TransactionSyncStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await ctx.Service.AddAsync(transaction, isOnline: false);

        // Record sum of all account balances after creation (before deletion)
        decimal sumBeforeDelete;
        using (var db = await ctx.Factory.CreateDbContextAsync())
        {
            var accounts = await db.Account.ToListAsync();
            sumBeforeDelete = accounts.Sum(a => a.CurrentBalance);
        }

        // Get the persisted transaction ID for deletion
        int transactionId;
        using (var db = await ctx.Factory.CreateDbContextAsync())
        {
            var persisted = await db.Transaction.FirstAsync();
            transactionId = persisted.Id;
        }

        // Delete (soft-delete) the transfer
        await ctx.Service.DeleteAsync(transactionId, isOnline: false);

        // Sum of all account balances after deletion
        decimal sumAfterDelete;
        using (var db = await ctx.Factory.CreateDbContextAsync())
        {
            var accounts = await db.Account.ToListAsync();
            sumAfterDelete = accounts.Sum(a => a.CurrentBalance);
        }

        return sumBeforeDelete == sumAfterDelete;
    }
}
