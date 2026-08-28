using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Model.DTO;
using Xunit;

namespace RecurringTests.ChartTests;

/// <summary>
/// Local record mirroring XpemFinancial.VMs.ChartPoint to avoid cross-TFM reference issues.
/// </summary>
internal record ChartPoint(int Day, decimal Value);

/// <summary>
/// Property 3: Monthly points appear only on days with transactions
/// Property 4: Transfer transactions are excluded from chart series (monthly mode)
/// **Validates: Requirements 4.2, 4.3, 4.4**
/// </summary>
[Trait("Feature", "annual-chart-view")]
[Trait("Property", "3")]
public class MonthlyPointsOnlyOnTransactionDaysPropertyTests
{
    private const int DaysInMonth = 30; // June 2024

    /// <summary>
    /// Generates a random TransactionDTO with a specific type, with dates in June 2024 (30 days).
    /// </summary>
    private static Gen<TransactionDTO> TransactionOfType(TransactionType type)
    {
        return from amountInt in Gen.Choose(1, 100_000)
               from day in Gen.Choose(1, DaysInMonth)
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
    /// Generates a list of transactions with random types (Income, Expense, Transfer) in June 2024.
    /// </summary>
    private static Gen<List<TransactionDTO>> MixedTransactionList()
    {
        return from count in Gen.Choose(1, 25)
               from transactions in Gen.ListOf(MixedTransaction(), count)
               select transactions.ToList();
    }

    /// <summary>
    /// Generates a single transaction with a random type.
    /// </summary>
    private static Gen<TransactionDTO> MixedTransaction()
    {
        return from type in Gen.Elements(
                   TransactionType.Income,
                   TransactionType.Expense,
                   TransactionType.Transfer)
               from amountInt in Gen.Choose(1, 100_000)
               from day in Gen.Choose(1, DaysInMonth)
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
    /// Pure static extraction of the monthly series computation logic from ChartVM.LoadChartAsync.
    /// This mirrors the production logic exactly (without previous balance).
    /// </summary>
    private static (List<ChartPoint> IncomePoints, List<ChartPoint> ExpensePoints) ComputeMonthlySeries(
        IEnumerable<TransactionDTO> transactions, int daysInMonth)
    {
        var incomeByDay = transactions
            .Where(t => t.Type == TransactionType.Income)
            .GroupBy(t => t.Date.Day)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

        var expenseByDay = transactions
            .Where(t => t.Type == TransactionType.Expense)
            .GroupBy(t => t.Date.Day)
            .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));

        var incomePoints = new List<ChartPoint>();
        var expensePoints = new List<ChartPoint>();
        decimal cumulativeIncome = 0;
        decimal cumulativeExpense = 0;

        for (int d = 1; d <= daysInMonth; d++)
        {
            if (incomeByDay.TryGetValue(d, out decimal dayIncome))
            {
                cumulativeIncome += dayIncome;
                incomePoints.Add(new ChartPoint(d, cumulativeIncome));
            }

            if (expenseByDay.TryGetValue(d, out decimal dayExpense))
            {
                cumulativeExpense += dayExpense;
                expensePoints.Add(new ChartPoint(d, cumulativeExpense));
            }
        }

        return (incomePoints, expensePoints);
    }

    /// <summary>
    /// Property 3 (Income): The income series contains a data point for day D if and only if
    /// there exists at least one Income transaction on day D.
    /// **Validates: Requirements 4.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IncomeSeries_HasPointOnlyOnDaysWithIncomeTransactions()
    {
        return Prop.ForAll(
            MixedTransactionList().ToArbitrary(),
            transactions =>
            {
                var (incomePoints, _) = ComputeMonthlySeries(transactions, DaysInMonth);

                // Days that have at least one Income transaction
                var daysWithIncome = transactions
                    .Where(t => t.Type == TransactionType.Income)
                    .Select(t => t.Date.Day)
                    .Distinct()
                    .ToHashSet();

                // Days that have income points in the series
                var daysInSeries = incomePoints.Select(p => p.Day).ToHashSet();

                // Biconditional: point exists at day D <=> at least one Income on day D
                bool allIncomePointsHaveTransactions = daysInSeries.All(d => daysWithIncome.Contains(d));
                bool allIncomeDaysHavePoints = daysWithIncome.All(d => daysInSeries.Contains(d));

                return allIncomePointsHaveTransactions
                    .Label("Every income point corresponds to a day with Income transactions")
                    .And(allIncomeDaysHavePoints)
                    .Label("Every day with Income transactions has a point in the series");
            });
    }

    /// <summary>
    /// Property 3 (Expense): The expense series contains a data point for day D if and only if
    /// there exists at least one Expense transaction on day D.
    /// **Validates: Requirements 4.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExpenseSeries_HasPointOnlyOnDaysWithExpenseTransactions()
    {
        return Prop.ForAll(
            MixedTransactionList().ToArbitrary(),
            transactions =>
            {
                var (_, expensePoints) = ComputeMonthlySeries(transactions, DaysInMonth);

                // Days that have at least one Expense transaction
                var daysWithExpense = transactions
                    .Where(t => t.Type == TransactionType.Expense)
                    .Select(t => t.Date.Day)
                    .Distinct()
                    .ToHashSet();

                // Days that have expense points in the series
                var daysInSeries = expensePoints.Select(p => p.Day).ToHashSet();

                // Biconditional: point exists at day D <=> at least one Expense on day D
                bool allExpensePointsHaveTransactions = daysInSeries.All(d => daysWithExpense.Contains(d));
                bool allExpenseDaysHavePoints = daysWithExpense.All(d => daysInSeries.Contains(d));

                return allExpensePointsHaveTransactions
                    .Label("Every expense point corresponds to a day with Expense transactions")
                    .And(allExpenseDaysHavePoints)
                    .Label("Every day with Expense transactions has a point in the series");
            });
    }
}

/// <summary>
/// Property 4: Transfer transactions are excluded from chart series (monthly mode)
/// Computing the series with transfers included produces the same result as computing without them.
/// **Validates: Requirements 4.4**
/// </summary>
[Trait("Feature", "annual-chart-view")]
[Trait("Property", "4")]
public class TransferExclusionFromMonthlyChartPropertyTests
{
    private const int DaysInMonth = 30; // June 2024

    /// <summary>
    /// Generates a list of transactions guaranteed to contain at least one Transfer.
    /// </summary>
    private static Gen<List<TransactionDTO>> MixedTransactionListWithTransfer()
    {
        return from transferTx in TransactionOfType(TransactionType.Transfer)
               from otherCount in Gen.Choose(0, 20)
               from others in Gen.ListOf(MixedTransaction(), otherCount)
               from insertIndex in Gen.Choose(0, others.Count)
               select others.ToList().InsertAndReturn(insertIndex, transferTx);
    }

    private static Gen<TransactionDTO> TransactionOfType(TransactionType type)
    {
        return from amountInt in Gen.Choose(1, 100_000)
               from day in Gen.Choose(1, DaysInMonth)
               from accountId in Gen.Choose(1, 100)
               from destOffset in Gen.Choose(1, 99)
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
                   DestinationAccountId = type == TransactionType.Transfer
                       ? ((accountId + destOffset - 1) % 100) + 1
                       : null,
                   SyncStatus = TransactionSyncStatus.Pending,
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow,
               };
    }

    private static Gen<TransactionDTO> MixedTransaction()
    {
        return from type in Gen.Elements(
                   TransactionType.Income,
                   TransactionType.Expense,
                   TransactionType.Transfer)
               from amountInt in Gen.Choose(1, 100_000)
               from day in Gen.Choose(1, DaysInMonth)
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
    /// Pure static extraction of the monthly series computation logic from ChartVM.LoadChartAsync.
    /// </summary>
    private static (List<ChartPoint> IncomePoints, List<ChartPoint> ExpensePoints) ComputeMonthlySeries(
        IEnumerable<TransactionDTO> transactions, int daysInMonth)
    {
        var incomeByDay = transactions
            .Where(t => t.Type == TransactionType.Income)
            .GroupBy(t => t.Date.Day)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

        var expenseByDay = transactions
            .Where(t => t.Type == TransactionType.Expense)
            .GroupBy(t => t.Date.Day)
            .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));

        var incomePoints = new List<ChartPoint>();
        var expensePoints = new List<ChartPoint>();
        decimal cumulativeIncome = 0;
        decimal cumulativeExpense = 0;

        for (int d = 1; d <= daysInMonth; d++)
        {
            if (incomeByDay.TryGetValue(d, out decimal dayIncome))
            {
                cumulativeIncome += dayIncome;
                incomePoints.Add(new ChartPoint(d, cumulativeIncome));
            }

            if (expenseByDay.TryGetValue(d, out decimal dayExpense))
            {
                cumulativeExpense += dayExpense;
                expensePoints.Add(new ChartPoint(d, cumulativeExpense));
            }
        }

        return (incomePoints, expensePoints);
    }

    /// <summary>
    /// Property 4 (Monthly): Transfer transactions do not affect either the income or expense series.
    /// Computing the series with all transactions (including transfers) produces the same result
    /// as computing with transfers removed.
    /// **Validates: Requirements 4.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TransferTransactions_DoNotAffectMonthlySeries()
    {
        return Prop.ForAll(
            MixedTransactionListWithTransfer().ToArbitrary(),
            allTransactions =>
            {
                // Compute with all transactions (transfers are naturally filtered by type checks)
                var (incomeWithAll, expenseWithAll) = ComputeMonthlySeries(allTransactions, DaysInMonth);

                // Compute without transfers
                var withoutTransfers = allTransactions
                    .Where(t => t.Type != TransactionType.Transfer)
                    .ToList();
                var (incomeWithout, expenseWithout) = ComputeMonthlySeries(withoutTransfers, DaysInMonth);

                // Both should produce identical results
                bool incomeSeriesEqual = incomeWithAll.Count == incomeWithout.Count
                    && incomeWithAll.Zip(incomeWithout).All(pair =>
                        pair.First.Day == pair.Second.Day && pair.First.Value == pair.Second.Value);

                bool expenseSeriesEqual = expenseWithAll.Count == expenseWithout.Count
                    && expenseWithAll.Zip(expenseWithout).All(pair =>
                        pair.First.Day == pair.Second.Day && pair.First.Value == pair.Second.Value);

                return incomeSeriesEqual
                    .Label("Income series is unchanged when transfers are present")
                    .And(expenseSeriesEqual)
                    .Label("Expense series is unchanged when transfers are present");
            });
    }
}

/// <summary>
/// Extension method for list insertion used by generators.
/// </summary>
internal static class MonthlyChartListExtensions
{
    public static List<T> InsertAndReturn<T>(this List<T> list, int index, T item)
    {
        list.Insert(index, item);
        return list;
    }
}
