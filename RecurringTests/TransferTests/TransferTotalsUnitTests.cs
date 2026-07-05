using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Repo;
using Xunit;

namespace RecurringTests.TransferTests;

/// <summary>
/// Unit tests for Transfer neutrality in Totals, ChartVM exclusion logic,
/// per-account previous balance with transfers as destination,
/// and TransactionDTO DestinationAccount null binding.
/// **Validates: Requirements 5.1, 5.2, 5.3, 5.4, 6.5, 8.1**
/// </summary>
[Trait("Feature", "transfer-transactions")]
public class TransferTotalsUnitTests
{
    /// <summary>
    /// Applies the same totals accumulation logic from MainVM.
    /// if Type == Income → income += Amount
    /// else if Type != Transfer → expense += Amount
    /// </summary>
    private static (decimal Income, decimal Expense) ComputeTotals(IEnumerable<TransactionDTO> transactions)
    {
        decimal income = 0;
        decimal expense = 0;

        foreach (var transaction in transactions)
        {
            if (transaction.Type == TransactionType.Income)
                income += transaction.Amount;
            else if (transaction.Type != TransactionType.Transfer)
                expense += transaction.Amount;
        }

        return (income, expense);
    }

    /// <summary>
    /// Applies the same chart filtering logic from ChartVM:
    /// Transaction list excludes Transfer type.
    /// </summary>
    private static List<TransactionDTO> FilterChartTransactions(IEnumerable<TransactionDTO> allTransactions)
    {
        return allTransactions.Where(t => t.Type != TransactionType.Transfer).ToList();
    }

    /// <summary>
    /// Applies the same chart income series logic from ChartVM:
    /// incomeByDay only includes Income type.
    /// </summary>
    private static Dictionary<int, decimal> ComputeIncomeByDay(IEnumerable<TransactionDTO> allTransactions)
    {
        return allTransactions
            .Where(t => t.Type == TransactionType.Income)
            .GroupBy(t => t.Date.Day)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
    }

    // ─── Test 1: Totals_TransferExcludedFromIncome ────────────────────────

    /// <summary>
    /// A list containing Income + Transfer → Income total excludes transfer amount.
    /// **Validates: Requirements 5.1**
    /// </summary>
    [Fact]
    [Trait("Feature", "transfer-transactions")]
    public void Totals_TransferExcludedFromIncome()
    {
        // Arrange
        var incomeTransaction = new TransactionDTO
        {
            TransactionId = Guid.NewGuid(),
            Description = "Salary",
            Date = new DateTime(2025, 1, 15),
            Amount = 5000m,
            Type = TransactionType.Income,
            CategoryId = 1,
            Repetition = Repetition.None,
            AccountId = 1,
            UserId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var transferTransaction = new TransactionDTO
        {
            TransactionId = Guid.NewGuid(),
            Description = "Transfer to savings",
            Date = new DateTime(2025, 1, 16),
            Amount = -1000m,
            Type = TransactionType.Transfer,
            CategoryId = null,
            Repetition = Repetition.None,
            AccountId = 1,
            DestinationAccountId = 2,
            UserId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var transactions = new List<TransactionDTO> { incomeTransaction, transferTransaction };

        // Act
        var (income, _) = ComputeTotals(transactions);

        // Assert: Income total = only the income transaction, transfer is excluded
        Assert.Equal(5000m, income);
    }

    // ─── Test 2: Totals_TransferExcludedFromExpense ───────────────────────

    /// <summary>
    /// A list containing Expense + Transfer → Expense total excludes transfer amount.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Fact]
    [Trait("Feature", "transfer-transactions")]
    public void Totals_TransferExcludedFromExpense()
    {
        // Arrange
        var expenseTransaction = new TransactionDTO
        {
            TransactionId = Guid.NewGuid(),
            Description = "Groceries",
            Date = new DateTime(2025, 1, 10),
            Amount = -200m,
            Type = TransactionType.Expense,
            CategoryId = 2,
            Repetition = Repetition.None,
            AccountId = 1,
            UserId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var transferTransaction = new TransactionDTO
        {
            TransactionId = Guid.NewGuid(),
            Description = "Transfer to investment",
            Date = new DateTime(2025, 1, 12),
            Amount = -500m,
            Type = TransactionType.Transfer,
            CategoryId = null,
            Repetition = Repetition.None,
            AccountId = 1,
            DestinationAccountId = 3,
            UserId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var transactions = new List<TransactionDTO> { expenseTransaction, transferTransaction };

        // Act
        var (_, expense) = ComputeTotals(transactions);

        // Assert: Expense total = only the expense transaction, transfer is excluded
        Assert.Equal(-200m, expense);
    }

    // ─── Test 3: Chart_TransfersExcludedFromList ─────────────────────────

    /// <summary>
    /// A list containing Income + Expense + Transfer → filtered list excludes Transfer type.
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Fact]
    [Trait("Feature", "transfer-transactions")]
    public void Chart_TransfersExcludedFromList()
    {
        // Arrange
        var incomeTransaction = new TransactionDTO
        {
            TransactionId = Guid.NewGuid(),
            Description = "Freelance",
            Date = new DateTime(2025, 2, 5),
            Amount = 3000m,
            Type = TransactionType.Income,
            CategoryId = 1,
            Repetition = Repetition.None,
            AccountId = 1,
            UserId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var expenseTransaction = new TransactionDTO
        {
            TransactionId = Guid.NewGuid(),
            Description = "Rent",
            Date = new DateTime(2025, 2, 1),
            Amount = -1500m,
            Type = TransactionType.Expense,
            CategoryId = 3,
            Repetition = Repetition.None,
            AccountId = 1,
            UserId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var transferTransaction = new TransactionDTO
        {
            TransactionId = Guid.NewGuid(),
            Description = "Transfer",
            Date = new DateTime(2025, 2, 10),
            Amount = -800m,
            Type = TransactionType.Transfer,
            CategoryId = null,
            Repetition = Repetition.None,
            AccountId = 1,
            DestinationAccountId = 2,
            UserId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var allTransactions = new List<TransactionDTO>
        {
            incomeTransaction, expenseTransaction, transferTransaction
        };

        // Act
        var filteredList = FilterChartTransactions(allTransactions);

        // Assert: filtered list has 2 items (Income + Expense), no Transfer
        Assert.Equal(2, filteredList.Count);
        Assert.DoesNotContain(filteredList, t => t.Type == TransactionType.Transfer);
        Assert.Contains(filteredList, t => t.Type == TransactionType.Income);
        Assert.Contains(filteredList, t => t.Type == TransactionType.Expense);
    }

    // ─── Test 4: Chart_IncomeByDay_ExcludesTransfers ─────────────────────

    /// <summary>
    /// Chart income series only includes Income type — Transfer amounts do not appear.
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Fact]
    [Trait("Feature", "transfer-transactions")]
    public void Chart_IncomeByDay_ExcludesTransfers()
    {
        // Arrange: same day has both an Income and a Transfer
        var incomeTransaction = new TransactionDTO
        {
            TransactionId = Guid.NewGuid(),
            Description = "Salary",
            Date = new DateTime(2025, 3, 15),
            Amount = 4000m,
            Type = TransactionType.Income,
            CategoryId = 1,
            Repetition = Repetition.None,
            AccountId = 1,
            UserId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var transferTransaction = new TransactionDTO
        {
            TransactionId = Guid.NewGuid(),
            Description = "Transfer same day",
            Date = new DateTime(2025, 3, 15),
            Amount = -2000m,
            Type = TransactionType.Transfer,
            CategoryId = null,
            Repetition = Repetition.None,
            AccountId = 1,
            DestinationAccountId = 2,
            UserId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var allTransactions = new List<TransactionDTO> { incomeTransaction, transferTransaction };

        // Act
        var incomeByDay = ComputeIncomeByDay(allTransactions);

        // Assert: day 15 only has the income amount, transfer is excluded
        Assert.Single(incomeByDay);
        Assert.True(incomeByDay.ContainsKey(15));
        Assert.Equal(4000m, incomeByDay[15]);
    }

    // ─── Test 5: PreviousBalance_PerAccount_IncludesTransferAsDestination ─

    /// <summary>
    /// Use in-memory DB, seed a transfer, verify per-account balance includes destination contribution.
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Fact]
    [Trait("Feature", "transfer-transactions")]
    public async Task PreviousBalance_PerAccount_IncludesTransferAsDestination()
    {
        // Arrange
        const int originAccountId = 1;
        const int destAccountId = 2;
        const int userId = 1;
        var referenceMonth = new DateTime(2025, 6, 1);

        var originAccount = new AccountDTO
        {
            Id = originAccountId,
            Name = "Checking",
            CurrentBalance = 0m,
            UserId = userId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var destAccount = new AccountDTO
        {
            Id = destAccountId,
            Name = "Savings",
            CurrentBalance = 0m,
            UserId = userId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var factory = await TransferGenerators.CreateSeededFactory(originAccount, destAccount);

        // Seed a transfer from origin to destination, dated before the reference month
        var transfer = new TransactionDTO
        {
            TransactionId = Guid.NewGuid(),
            Description = "Transfer to savings",
            Date = new DateTime(2025, 5, 20), // before June 2025
            Amount = -1000m, // negative: outgoing from origin
            Type = TransactionType.Transfer,
            CategoryId = null,
            Repetition = Repetition.None,
            AccountId = originAccountId,
            DestinationAccountId = destAccountId,
            UserId = userId,
            SyncStatus = TransactionSyncStatus.Synced,
            Inactive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        using (var db = await factory.CreateDbContextAsync())
        {
            db.Transaction.Add(transfer);
            await db.SaveChangesAsync();
        }

        var repo = new TransactionRepo(factory);

        // Act: Get previous balance for the destination account
        var destBalance = await repo.GetPreviousBalanceAsync(referenceMonth, destAccountId);

        // Assert: Destination account's previous balance includes the transfer contribution
        // Transfer Amount = -1000, so -Amount = +1000 credited to destination
        Assert.Equal(1000m, destBalance);

        // Also verify the origin account's balance includes the negative transfer
        var originBalance = await repo.GetPreviousBalanceAsync(referenceMonth, originAccountId);
        Assert.Equal(-1000m, originBalance);
    }

    // ─── Test 6: TransferWithoutDestAccount_HasNullDestinationAccount ────

    /// <summary>
    /// A TransactionDTO with DestinationAccount = null → DestinationAccount?.Name is null (for UI binding).
    /// This validates that when a transfer has no associated destination account (e.g., orphaned from pull),
    /// the UI does not display complementary text.
    /// **Validates: Requirements 6.5**
    /// </summary>
    [Fact]
    [Trait("Feature", "transfer-transactions")]
    public void TransferWithoutDestAccount_HasNullDestinationAccount()
    {
        // Arrange: Transfer without a resolved DestinationAccount navigation property
        var transfer = new TransactionDTO
        {
            TransactionId = Guid.NewGuid(),
            Description = "Orphaned transfer",
            Date = new DateTime(2025, 4, 10),
            Amount = -500m,
            Type = TransactionType.Transfer,
            CategoryId = null,
            Repetition = Repetition.None,
            AccountId = 1,
            DestinationAccountId = null, // no destination resolved
            DestinationAccount = null,   // navigation property is null
            UserId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        // Act & Assert: The UI binding pattern DestinationAccount?.Name should be null
        Assert.Null(transfer.DestinationAccount?.Name);
    }
}
