using FsCheck.Xunit;
using Model.DTO;
using Xunit;

namespace RecurringTests.TransferTests;

/// <summary>
/// Property 3: Dual balance impact on transfer creation
/// For any transfer of value V (where V > 0) between two accounts,
/// after creation the origin account balance SHALL decrease by V
/// and the destination account balance SHALL increase by V.
/// **Validates: Requirements 4.1, 4.2**
/// </summary>
[Trait("Feature", "transfer-transactions")]
[Trait("Property", "3")]
public class DualBalanceImpactPropertyTests
{
    /// <summary>
    /// For any positive amount V and two distinct accounts (starting with no prior transactions),
    /// when a transfer of V is created via AddAsync, the origin account balance becomes -V
    /// and the destination account balance becomes +V.
    /// **Validates: Requirements 4.1, 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> TransferCreation_DecreasesOriginAndIncreasesDestination(
        int amountCents, int originId, int destOffset, int userId)
    {
        // Constrain inputs to valid ranges
        if (amountCents <= 0 || amountCents > 100_000) return true; // skip trivial
        if (originId <= 0 || originId > 500) return true;
        if (destOffset <= 0 || destOffset > 499) return true;
        if (userId <= 0 || userId > 1000) return true;

        decimal positiveAmount = amountCents / 100m;
        int destId = ((originId + destOffset - 1) % 1000) + 1;

        // Arrange: accounts start with CurrentBalance = 0 (no prior transactions)
        var origin = new AccountDTO
        {
            Id = originId,
            Name = $"Account {originId}",
            CurrentBalance = 0m,
            UserId = userId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var destination = new AccountDTO
        {
            Id = destId,
            Name = $"Account {destId}",
            CurrentBalance = 0m,
            UserId = userId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var ctx = await TransferGenerators.CreateServiceContext(origin, destination);

        var transaction = new TransactionDTO
        {
            TransactionId = Guid.NewGuid(),
            UserId = userId,
            Description = "Transfer balance test",
            Date = DateTime.UtcNow,
            Amount = positiveAmount, // positive input, service negates it
            Type = TransactionType.Transfer,
            CategoryId = null,
            Repetition = Repetition.None,
            AccountId = origin.Id,
            DestinationAccountId = destination.Id,
            SyncStatus = TransactionSyncStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        // Act
        await ctx.Service.AddAsync(transaction, isOnline: false);

        // Assert: The RecalculateAccountBalanceAsync method calls accountRepo.GetByIdAsync
        // which returns the original object references, then sets CurrentBalance = sum
        // and calls accountRepo.Update(). Since accountRepo is a mock, the balance
        // is updated on the object reference directly.
        // We verify by checking what balance was set via accountRepo.Update() calls.
        var updatedOriginBalance = origin.CurrentBalance;
        var updatedDestinationBalance = destination.CurrentBalance;

        // After adding a transfer of V with no prior transactions:
        // Origin balance should be -V (the transfer Amount is stored as -V)
        // Destination balance should be +V (GetSumByAccountIdAsync computes -Amount for dest)
        bool originDecreasedByV = updatedOriginBalance == -positiveAmount;
        bool destinationIncreasedByV = updatedDestinationBalance == positiveAmount;

        return originDecreasedByV && destinationIncreasedByV;
    }
}
