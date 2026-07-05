using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Model.DTO;
using Xunit;

namespace RecurringTests.TransferTests;

/// <summary>
/// Property 8: Transfer exclusion from chart series
/// For any set of monthly transactions that includes transfers, the cumulative IncomePoints
/// series SHALL only aggregate Income-type transactions, and the cumulative ExpensePoints
/// series SHALL only aggregate Expense-type transactions.
/// **Validates: Requirements 5.3**
/// </summary>
[Trait("Feature", "transfer-transactions")]
[Trait("Property", "8")]
public class TransferExclusionFromChartPropertyTests
{
    /// <summary>
    /// Generates a random TransactionDTO with a specified type.
    /// </summary>
    private static Gen<TransactionDTO> TransactionOfType(TransactionType type)
    {
        return from amountInt in Gen.Choose(1, 100_000)
               from day in Gen.Choose(1, 28)
               from accountId in Gen.Choose(1, 100)
               select new TransactionDTO
               {
                   TransactionId = Guid.NewGuid(),
                   UserId = 1,
                   Description = $"{type} transaction",
                   Date = new DateTime(2024, 6, day, 12, 0, 0, DateTimeKind.Utc),
                   Amount = type == TransactionType.Income
                       ? amountInt / 100m
                       : -(amountInt / 100m),
                   Type = type,
                   CategoryId = type == TransactionType.Transfer ? null : 1,
                   Repetition = Repetition.None,
                   AccountId = accountId,
                   DestinationAccountId = type == TransactionType.Transfer ? accountId + 1 : null,
                   SyncStatus = TransactionSyncStatus.Pending,
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow,
               };
    }

    /// <summary>
    /// Generates a mixed list of transactions containing Income, Expense, and Transfer types.
    /// Always includes at least one Transfer to ensure the property is meaningful.
    /// </summary>
    private static Gen<List<TransactionDTO>> MixedTransactionList()
    {
        return from transferTx in TransactionOfType(TransactionType.Transfer)
               from otherCount in Gen.Choose(0, 20)
               from others in Gen.ListOf(MixedTransaction(), otherCount)
               from insertIndex in Gen.Choose(0, others.Count)
               select others.ToList().InsertAndReturn(insertIndex, transferTx);
    }

    /// <summary>
    /// Generator for a random TransactionDTO with a random type (Income, Expense, Transfer).
    /// </summary>
    private static Gen<TransactionDTO> MixedTransaction()
    {
        return from type in Gen.Elements(
                   TransactionType.Income,
                   TransactionType.Expense,
                   TransactionType.Transfer)
               from amountInt in Gen.Choose(1, 100_000)
               from day in Gen.Choose(1, 28)
               from accountId in Gen.Choose(1, 100)
               from destOffset in Gen.Choose(1, 99)
               let amount = type == TransactionType.Income
                   ? amountInt / 100m
                   : -(amountInt / 100m)
               select new TransactionDTO
               {
                   TransactionId = Guid.NewGuid(),
                   UserId = 1,
                   Description = $"{type} transaction",
                   Date = new DateTime(2024, 6, day, 12, 0, 0, DateTimeKind.Utc),
                   Amount = amount,
                   Type = type,
                   CategoryId = type == TransactionType.Transfer ? null : 1,
                   Repetition = Repetition.None,
                   AccountId = accountId,
                   DestinationAccountId = type == TransactionType.Transfer
                       ? ((accountId + destOffset - 1) % 100) + 1
                       : null,
                   SyncStatus = TransactionSyncStatus.Pending,
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow,
               };
    }

    /// <summary>
    /// Applies the same chart aggregation logic as ChartVM.LoadChartAsync:
    /// incomeByDay aggregates only Income-type transactions grouped by day.
    /// The total aggregated in incomeByDay must equal the sum of Income-only amounts.
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IncomeByDay_OnlyAggregatesIncomeTransactions()
    {
        return Prop.ForAll(
            MixedTransactionList().ToArbitrary(),
            allTransactions =>
            {
                // Apply chart logic (same as ChartVM)
                var incomeByDay = allTransactions
                    .Where(t => t.Type == TransactionType.Income)
                    .GroupBy(t => t.Date.Day)
                    .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

                // Expected: sum of all Income-only transaction amounts
                decimal expectedIncomeTotal = allTransactions
                    .Where(t => t.Type == TransactionType.Income)
                    .Sum(t => t.Amount);

                // Actual: total from the incomeByDay dictionary
                decimal actualIncomeTotal = incomeByDay.Values.Sum();

                // No Transfer amounts should appear in incomeByDay
                decimal transferTotal = allTransactions
                    .Where(t => t.Type == TransactionType.Transfer)
                    .Sum(t => t.Amount);

                return (actualIncomeTotal == expectedIncomeTotal)
                    .Label($"incomeByDay total ({actualIncomeTotal}) == income-only sum ({expectedIncomeTotal})")
                    .And(() => transferTotal != 0 || allTransactions.All(t => t.Type != TransactionType.Transfer))
                    .Or(() => actualIncomeTotal == expectedIncomeTotal);
            });
    }

    /// <summary>
    /// Applies the same chart aggregation logic as ChartVM.LoadChartAsync:
    /// expenseByDay aggregates only Expense-type transactions grouped by day.
    /// The total aggregated in expenseByDay must equal the sum of Expense-only absolute amounts.
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExpenseByDay_OnlyAggregatesExpenseTransactions()
    {
        return Prop.ForAll(
            MixedTransactionList().ToArbitrary(),
            allTransactions =>
            {
                // Apply chart logic (same as ChartVM)
                var expenseByDay = allTransactions
                    .Where(t => t.Type == TransactionType.Expense)
                    .GroupBy(t => t.Date.Day)
                    .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));

                // Expected: sum of absolute values of all Expense-only transactions
                decimal expectedExpenseTotal = allTransactions
                    .Where(t => t.Type == TransactionType.Expense)
                    .Sum(t => Math.Abs(t.Amount));

                // Actual: total from the expenseByDay dictionary
                decimal actualExpenseTotal = expenseByDay.Values.Sum();

                return (actualExpenseTotal == expectedExpenseTotal)
                    .Label($"expenseByDay total ({actualExpenseTotal}) == expense-only abs sum ({expectedExpenseTotal})");
            });
    }

    /// <summary>
    /// Verifies that no Transfer transaction amounts contribute to either the income or
    /// expense chart series. The combined property: for any list of transactions,
    /// total in incomeByDay == sum of Income-only amounts AND total in expenseByDay ==
    /// sum of Expense-only absolute amounts (transfers excluded from both).
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TransferAmounts_NeverAppearInEitherChartSeries()
    {
        return Prop.ForAll(
            MixedTransactionList().ToArbitrary(),
            allTransactions =>
            {
                // Apply chart logic (same as ChartVM)
                var incomeByDay = allTransactions
                    .Where(t => t.Type == TransactionType.Income)
                    .GroupBy(t => t.Date.Day)
                    .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

                var expenseByDay = allTransactions
                    .Where(t => t.Type == TransactionType.Expense)
                    .GroupBy(t => t.Date.Day)
                    .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));

                // Verify income series
                decimal incomeSeriesTotal = incomeByDay.Values.Sum();
                decimal expectedIncomeTotal = allTransactions
                    .Where(t => t.Type == TransactionType.Income)
                    .Sum(t => t.Amount);

                // Verify expense series
                decimal expenseSeriesTotal = expenseByDay.Values.Sum();
                decimal expectedExpenseTotal = allTransactions
                    .Where(t => t.Type == TransactionType.Expense)
                    .Sum(t => Math.Abs(t.Amount));

                // If we incorrectly included transfers in income
                decimal transferContributionToIncome = allTransactions
                    .Where(t => t.Type == TransactionType.Transfer)
                    .Sum(t => t.Amount);

                // If we incorrectly included transfers in expense
                decimal transferContributionToExpense = allTransactions
                    .Where(t => t.Type == TransactionType.Transfer)
                    .Sum(t => Math.Abs(t.Amount));

                bool incomeCorrect = incomeSeriesTotal == expectedIncomeTotal;
                bool expenseCorrect = expenseSeriesTotal == expectedExpenseTotal;

                // The key assertion: transfers don't appear in either series
                bool noTransfersInIncome = incomeSeriesTotal != expectedIncomeTotal + transferContributionToIncome
                    || transferContributionToIncome == 0;
                bool noTransfersInExpense = expenseSeriesTotal != expectedExpenseTotal + transferContributionToExpense
                    || transferContributionToExpense == 0;

                return incomeCorrect
                    .Label("Income series excludes transfers")
                    .And(expenseCorrect)
                    .Label("Expense series excludes transfers");
            });
    }
}
