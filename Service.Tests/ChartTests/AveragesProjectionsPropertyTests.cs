using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Model.DTO;
using Xunit;

namespace RecurringTests.ChartTests;

/// <summary>
/// Property-based tests for the "Médias e Projeções" screen's pure computation logic.
/// Mirrors XpemFinancial.VMs.AveragesProjectionsVM.LoadAsync's aggregation/average/projection
/// logic as a pure static method (same approach as AnnualSeriesPropertyTests, to avoid
/// cross-project reference issues between Service.Tests and the XpemFinancial MAUI project).
/// </summary>
[Trait("Feature", "averages-projections")]
public class AveragesProjectionsPropertyTests
{
    private record ChartPoint(int Month, decimal Value);

    private const int HistoryMonths = 6;
    private const int ProjectionMonths = 3;

    // Fixed reference "today" so the 6-month window (Jan..Jun/2024) is deterministic across test runs.
    private static readonly DateTime Today = new(2024, 6, 15);

    /// <summary>
    /// Replicates AveragesProjectionsVM.LoadAsync's pure aggregation/average/projection logic,
    /// including excluding months with zero transactions of any type (e.g. before the user
    /// started using the app) from the median/outlier computation.
    /// </summary>
    private static (List<ChartPoint> IncomePoints, List<ChartPoint> ExpensePoints,
        decimal AverageIncome, decimal AverageExpense, decimal AverageBalance, decimal MaxValue,
        int? IncomeOutlierIndex, int? ExpenseOutlierIndex, int RealMonthsCount)
        ComputeAveragesProjections(IEnumerable<TransactionDTO> transactions)
    {
        var transactionList = transactions.ToList();
        var from = new DateTime(Today.Year, Today.Month, 1).AddMonths(-(HistoryMonths - 1));

        var months = Enumerable.Range(0, HistoryMonths)
            .Select(i => from.AddMonths(i))
            .ToList();

        var incomeByMonth = transactionList
            .Where(t => t.Type == TransactionType.Income)
            .GroupBy(t => (t.Date.Year, t.Date.Month))
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

        var expenseByMonth = transactionList
            .Where(t => t.Type == TransactionType.Expense)
            .GroupBy(t => (t.Date.Year, t.Date.Month))
            .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));

        var incomeTotals = months
            .Select(m => incomeByMonth.TryGetValue((m.Year, m.Month), out var v) ? v : 0)
            .ToList();

        var expenseTotals = months
            .Select(m => expenseByMonth.TryGetValue((m.Year, m.Month), out var v) ? v : 0)
            .ToList();

        var monthsWithData = transactionList
            .Select(t => (t.Date.Year, t.Date.Month))
            .ToHashSet();

        var realMonthIndices = Enumerable.Range(0, HistoryMonths)
            .Where(i => monthsWithData.Contains((months[i].Year, months[i].Month)))
            .ToList();

        var incomeTotalsForAverage = realMonthIndices.Select(i => incomeTotals[i]).ToList();
        var expenseTotalsForAverage = realMonthIndices.Select(i => expenseTotals[i]).ToList();

        decimal averageIncome = Median(incomeTotalsForAverage);
        decimal averageExpense = Median(expenseTotalsForAverage);
        decimal averageBalance = averageIncome - averageExpense;

        int? incomeOutlierPosition = FindOutlierIndex(incomeTotalsForAverage, averageIncome);
        int? expenseOutlierPosition = FindOutlierIndex(expenseTotalsForAverage, averageExpense);
        int? incomeOutlierIndex = incomeOutlierPosition.HasValue ? realMonthIndices[incomeOutlierPosition.Value - 1] + 1 : null;
        int? expenseOutlierIndex = expenseOutlierPosition.HasValue ? realMonthIndices[expenseOutlierPosition.Value - 1] + 1 : null;

        var incomePoints = new List<ChartPoint>();
        var expensePoints = new List<ChartPoint>();

        for (int i = 0; i < HistoryMonths; i++)
        {
            incomePoints.Add(new ChartPoint(i + 1, incomeTotals[i]));
            expensePoints.Add(new ChartPoint(i + 1, expenseTotals[i]));
        }

        for (int i = 0; i < ProjectionMonths; i++)
        {
            incomePoints.Add(new ChartPoint(HistoryMonths + i + 1, averageIncome));
            expensePoints.Add(new ChartPoint(HistoryMonths + i + 1, averageExpense));
        }

        var allValues = incomePoints.Select(p => p.Value).Concat(expensePoints.Select(p => p.Value));
        decimal maxValue = allValues.Any() ? Math.Max(allValues.Max(), 1) : 1;

        return (incomePoints, expensePoints, averageIncome, averageExpense, averageBalance, maxValue,
            incomeOutlierIndex, expenseOutlierIndex, realMonthIndices.Count);
    }

    /// <summary>
    /// Mirrors AveragesProjectionsVM.Median: resistant to a single out-of-curve month,
    /// unlike the arithmetic mean.
    /// </summary>
    private static decimal Median(List<decimal> values)
    {
        if (values.Count == 0) return 0m;

        var sorted = values.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2m
            : sorted[mid];
    }

    private const decimal OutlierDeviationRatio = 0.5m;

    /// <summary>
    /// Mirrors AveragesProjectionsVM.FindOutlierIndex: the "fora do padrão" ring shown on
    /// the chart for the historical month that deviates most from the median.
    /// </summary>
    private static int? FindOutlierIndex(List<decimal> monthlyTotals, decimal median)
    {
        if (median == 0) return null;

        int worstIndex = -1;
        decimal worstRatio = 0;

        for (int i = 0; i < monthlyTotals.Count; i++)
        {
            decimal ratio = Math.Abs(monthlyTotals[i] - median) / median;
            if (ratio > worstRatio)
            {
                worstRatio = ratio;
                worstIndex = i;
            }
        }

        return worstRatio > OutlierDeviationRatio ? worstIndex + 1 : null;
    }

    #region Generators

    /// <summary>
    /// Generates a random TransactionDTO with a specified type and positive amount,
    /// dated within the fixed 6-month window (Jan..Jun/2024).
    /// </summary>
    private static Gen<TransactionDTO> TransactionOfType(TransactionType type)
    {
        return from amountInt in Gen.Choose(1, 100_000)
               from month in Gen.Choose(1, 6)
               from day in Gen.Choose(1, 28)
               from accountId in Gen.Choose(1, 100)
               select new TransactionDTO
               {
                   TransactionId = Guid.NewGuid(),
                   UserId = 1,
                   Description = $"{type} transaction",
                   Date = new DateTime(2024, month, day, 12, 0, 0, DateTimeKind.Utc),
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
    /// Generates a random TransactionDTO with a random type (Income, Expense, Transfer, Adjustment)
    /// and a random Inactive flag, dated within the fixed 6-month window.
    /// </summary>
    private static Gen<TransactionDTO> RandomTransaction()
    {
        return from type in Gen.Elements(
                   TransactionType.Income,
                   TransactionType.Expense,
                   TransactionType.Transfer,
                   TransactionType.Adjustment)
               from amountInt in Gen.Choose(1, 100_000)
               from month in Gen.Choose(1, 6)
               from day in Gen.Choose(1, 28)
               from accountId in Gen.Choose(1, 100)
               from inactive in Gen.Elements(true, false)
               select new TransactionDTO
               {
                   TransactionId = Guid.NewGuid(),
                   UserId = 1,
                   Description = $"{type} transaction",
                   Date = new DateTime(2024, month, day, 12, 0, 0, DateTimeKind.Utc),
                   Amount = type == TransactionType.Income
                       ? amountInt / 100m
                       : -(amountInt / 100m),
                   Type = type,
                   CategoryId = type == TransactionType.Transfer ? null : 1,
                   Repetition = Repetition.None,
                   AccountId = accountId,
                   DestinationAccountId = type == TransactionType.Transfer ? accountId + 1 : null,
                   Inactive = inactive,
                   SyncStatus = TransactionSyncStatus.Pending,
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow,
               };
    }

    /// <summary>
    /// Generates a list of random transactions (0–30 items) with mixed types and Inactive flags.
    /// </summary>
    private static Gen<List<TransactionDTO>> RandomTransactionList()
    {
        return from count in Gen.Choose(0, 30)
               from transactions in Gen.ListOf(RandomTransaction(), count)
               select transactions.ToList();
    }

    /// <summary>
    /// An Income transaction pinned to a specific month — used to guarantee a month has data
    /// independent of whichever Transfer/Adjustment transaction also lands there, so removing
    /// Transfer/Adjustment types never changes which months count as "has data" (see
    /// TransactionListWithExclusions).
    /// </summary>
    private static Gen<TransactionDTO> IncomeAnchorInMonth(int month)
    {
        return from amountInt in Gen.Choose(1, 100_000)
               from day in Gen.Choose(1, 28)
               from accountId in Gen.Choose(1, 100)
               select new TransactionDTO
               {
                   TransactionId = Guid.NewGuid(),
                   UserId = 1,
                   Description = "Income anchor",
                   Date = new DateTime(2024, month, day, 12, 0, 0, DateTimeKind.Utc),
                   Amount = amountInt / 100m,
                   Type = TransactionType.Income,
                   CategoryId = 1,
                   Repetition = Repetition.None,
                   AccountId = accountId,
                   SyncStatus = TransactionSyncStatus.Pending,
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow,
               };
    }

    /// <summary>
    /// Generates a list of transactions that always includes at least one Transfer and one
    /// Adjustment, each paired with an Income anchor in the same month — guaranteeing that
    /// whether Transfer/Adjustment are included or stripped out, the set of "months with data"
    /// stays identical, so this test isolates "do Transfer/Adjustment amounts affect the
    /// totals" from "does a month's presence/absence affect the median" (a separate property).
    /// The "others" here are Income/Expense only for the same reason — a random Transfer or
    /// Adjustment among them could otherwise be the sole transaction of its month.
    /// Note: Inactive filtering is a repo-layer concern (see TransactionRepo.GetByDateRangeAsync's
    /// `!t.Inactive` clause, mirrored from GetByMonthYear/GetByYear) — by the time transactions
    /// reach this pure aggregation logic they are already assumed active, so it's not re-checked here.
    /// </summary>
    private static Gen<List<TransactionDTO>> TransactionListWithExclusions()
    {
        return from transferTx in TransactionOfType(TransactionType.Transfer)
               from adjustmentTx in TransactionOfType(TransactionType.Adjustment)
               from transferAnchor in IncomeAnchorInMonth(transferTx.Date.Month)
               from adjustmentAnchor in IncomeAnchorInMonth(adjustmentTx.Date.Month)
               from otherCount in Gen.Choose(0, 15)
               from others in Gen.ListOf(IncomeOrExpenseTransaction(), otherCount)
               select others
                   .Append(transferTx)
                   .Append(adjustmentTx)
                   .Append(transferAnchor)
                   .Append(adjustmentAnchor)
                   .ToList();
    }

    /// <summary>
    /// Generates a random Income or Expense transaction (never Transfer/Adjustment) within the
    /// fixed 6-month window — used by TransactionListWithExclusions to keep Transfer/Adjustment
    /// only in the two explicit, anchored positions.
    /// </summary>
    private static Gen<TransactionDTO> IncomeOrExpenseTransaction()
    {
        return from type in Gen.Elements(TransactionType.Income, TransactionType.Expense)
               from amountInt in Gen.Choose(1, 100_000)
               from month in Gen.Choose(1, 6)
               from day in Gen.Choose(1, 28)
               from accountId in Gen.Choose(1, 100)
               select new TransactionDTO
               {
                   TransactionId = Guid.NewGuid(),
                   UserId = 1,
                   Description = $"{type} transaction",
                   Date = new DateTime(2024, month, day, 12, 0, 0, DateTimeKind.Utc),
                   Amount = type == TransactionType.Income ? amountInt / 100m : -(amountInt / 100m),
                   Type = type,
                   CategoryId = 1,
                   Repetition = Repetition.None,
                   AccountId = accountId,
                   SyncStatus = TransactionSyncStatus.Pending,
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow,
               };
    }

    #endregion

    /// <summary>
    /// Property 1: Always produces exactly 6 historical points + 3 projected points (9 total)
    /// per series, for any set of transactions (including empty).
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "1")]
    public Property AlwaysProduces_Exactly6RealPlus3ProjectedPoints()
    {
        return Prop.ForAll(
            RandomTransactionList().ToArbitrary(),
            transactions =>
            {
                var result = ComputeAveragesProjections(transactions);

                return (result.IncomePoints.Count == HistoryMonths + ProjectionMonths)
                    .Label($"Income points count should be 9, was {result.IncomePoints.Count}")
                    .And(result.ExpensePoints.Count == HistoryMonths + ProjectionMonths)
                    .Label($"Expense points count should be 9, was {result.ExpensePoints.Count}");
            });
    }

    /// <summary>
    /// Property 2: The 3 projected points are always exactly equal to AverageIncome/AverageExpense
    /// (flat projection).
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "2")]
    public Property ProjectedPoints_AreAlwaysExactlyTheAverage()
    {
        return Prop.ForAll(
            RandomTransactionList().ToArbitrary(),
            transactions =>
            {
                var result = ComputeAveragesProjections(transactions);

                var projectedIncome = result.IncomePoints.Skip(HistoryMonths);
                var projectedExpense = result.ExpensePoints.Skip(HistoryMonths);

                bool incomeFlat = projectedIncome.All(p => p.Value == result.AverageIncome);
                bool expenseFlat = projectedExpense.All(p => p.Value == result.AverageExpense);

                return incomeFlat
                    .Label("All projected income points should equal AverageIncome")
                    .And(expenseFlat)
                    .Label("All projected expense points should equal AverageExpense");
            });
    }

    /// <summary>
    /// Property 3: AverageIncome/AverageExpense are always the median (not the arithmetic mean)
    /// of the monthly totals from months that actually have data — chosen so a single
    /// out-of-curve month (e.g. an extra vacation bonus) doesn't skew the average used for the
    /// projection, and so months with no transactions at all (e.g. before the user started
    /// using the app) don't drag the median toward zero.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "3")]
    public Property Averages_AreAlwaysTheMedianOfTheRealMonthlyTotals()
    {
        return Prop.ForAll(
            RandomTransactionList().ToArbitrary(),
            transactions =>
            {
                var result = ComputeAveragesProjections(transactions);

                var from = new DateTime(Today.Year, Today.Month, 1).AddMonths(-(HistoryMonths - 1));
                var monthsWithData = transactions.Select(t => (t.Date.Year, t.Date.Month)).ToHashSet();

                var realIndices = Enumerable.Range(0, HistoryMonths)
                    .Where(i => monthsWithData.Contains((from.AddMonths(i).Year, from.AddMonths(i).Month)))
                    .ToList();

                decimal expectedAvgIncome = Median(realIndices.Select(i => result.IncomePoints[i].Value).ToList());
                decimal expectedAvgExpense = Median(realIndices.Select(i => result.ExpensePoints[i].Value).ToList());

                return (result.AverageIncome == expectedAvgIncome)
                    .Label($"AverageIncome should be {expectedAvgIncome}, was {result.AverageIncome}")
                    .And(result.AverageExpense == expectedAvgExpense)
                    .Label($"AverageExpense should be {expectedAvgExpense}, was {result.AverageExpense}");
            });
    }

    /// <summary>
    /// Property 4: AverageBalance always equals AverageIncome - AverageExpense.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "4")]
    public Property AverageBalance_AlwaysEqualsIncomeMinusExpense()
    {
        return Prop.ForAll(
            RandomTransactionList().ToArbitrary(),
            transactions =>
            {
                var result = ComputeAveragesProjections(transactions);

                return (result.AverageBalance == result.AverageIncome - result.AverageExpense)
                    .Label($"AverageBalance should be {result.AverageIncome - result.AverageExpense}, was {result.AverageBalance}");
            });
    }

    /// <summary>
    /// Property 5: Transfer and Adjustment transactions never enter the totals — the
    /// computation SHALL produce the same result as if those transactions did not exist.
    /// (Inactive filtering is a repo-layer concern, not asserted here — see
    /// TransactionListWithExclusions' remarks.)
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "5")]
    public Property TransferAndAdjustment_AreExcluded_FromTotals()
    {
        return Prop.ForAll(
            TransactionListWithExclusions().ToArbitrary(),
            transactions =>
            {
                var withExclusions = ComputeAveragesProjections(transactions);

                var withoutExcluded = transactions
                    .Where(t => t.Type != TransactionType.Transfer
                             && t.Type != TransactionType.Adjustment)
                    .ToList();

                var withoutThem = ComputeAveragesProjections(withoutExcluded);

                bool incomeEqual = withExclusions.IncomePoints
                    .Zip(withoutThem.IncomePoints, (a, b) => a.Value == b.Value)
                    .All(x => x);

                bool expenseEqual = withExclusions.ExpensePoints
                    .Zip(withoutThem.ExpensePoints, (a, b) => a.Value == b.Value)
                    .All(x => x);

                return incomeEqual
                    .Label("Income series should be identical with/without excluded transactions")
                    .And(expenseEqual)
                    .Label("Expense series should be identical with/without excluded transactions");
            });
    }

    /// <summary>
    /// Property 6: MaxValue is always at least 1 (same divide-by-zero guard as ChartVM).
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "6")]
    public Property MaxValue_IsAlwaysAtLeast1()
    {
        return Prop.ForAll(
            RandomTransactionList().ToArbitrary(),
            transactions =>
            {
                var result = ComputeAveragesProjections(transactions);

                return (result.MaxValue >= 1m)
                    .Label($"MaxValue should be >= 1, was {result.MaxValue}");
            });
    }

    /// <summary>
    /// Property 7: The median is insensitive to inflating the single largest of the 6 monthly
    /// totals — exactly the behavior that motivated switching from mean to median: a one-off
    /// atypical month (e.g. an extra vacation bonus) should not distort the average used for
    /// the projection.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "7")]
    public Property Median_IsInsensitive_ToInflatingTheLargestMonth()
    {
        var sixAmounts = Gen.ListOf(Gen.Choose(1, 100_000), 6)
            .Select(amounts => amounts.Select(a => a / 100m).ToList());

        return Prop.ForAll(
            sixAmounts.ToArbitrary(),
            amounts =>
            {
                var baselineResult = ComputeAveragesProjections(BuildMonthlyIncomeTransactions(amounts));

                int maxIndex = amounts.IndexOf(amounts.Max());
                var inflatedAmounts = amounts.ToList();
                inflatedAmounts[maxIndex] += 1_000_000m; // e.g. an extra vacation bonus

                var inflatedResult = ComputeAveragesProjections(BuildMonthlyIncomeTransactions(inflatedAmounts));

                return (baselineResult.AverageIncome == inflatedResult.AverageIncome)
                    .Label($"Median should stay {baselineResult.AverageIncome} after inflating the largest month, but became {inflatedResult.AverageIncome}");
            });
    }

    /// <summary>
    /// Builds exactly one Income transaction per month of the fixed 6-month window, with the
    /// given amounts in month order — used to make Property 7's assertions exact/deterministic.
    /// </summary>
    private static List<TransactionDTO> BuildMonthlyIncomeTransactions(List<decimal> amounts)
    {
        var from = new DateTime(Today.Year, Today.Month, 1).AddMonths(-(HistoryMonths - 1));

        return Enumerable.Range(0, HistoryMonths)
            .Select(i =>
            {
                var month = from.AddMonths(i);
                return new TransactionDTO
                {
                    TransactionId = Guid.NewGuid(),
                    UserId = 1,
                    Description = "Income",
                    Date = new DateTime(month.Year, month.Month, 15, 12, 0, 0, DateTimeKind.Utc),
                    Amount = amounts[i],
                    Type = TransactionType.Income,
                    CategoryId = 1,
                    Repetition = Repetition.None,
                    AccountId = 1,
                    SyncStatus = TransactionSyncStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
            })
            .ToList();
    }

    /// <summary>
    /// Property 8: A month whose total deviates from the median by more than the configured
    /// threshold is flagged as the outlier — this is exactly the "fora do padrão" ring shown
    /// on the chart.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "8")]
    public Property FindOutlierIndex_FlagsAMonthThatDeviatesBeyondThreshold()
    {
        // 5 baseline months clustered near 1000; the 6th (inserted at a random position) is
        // way beyond the 50% threshold, so it should always be the one flagged.
        var baseline = new List<decimal> { 950m, 1000m, 1000m, 1050m, 1000m };

        return Prop.ForAll(
            Gen.Choose(0, 5).ToArbitrary(),
            outlierPosition =>
            {
                var amounts = baseline.ToList();
                amounts.Insert(outlierPosition, 5000m);

                int? result = FindOutlierIndex(amounts, Median(amounts));

                return (result == outlierPosition + 1)
                    .Label($"Expected outlier at index {outlierPosition + 1}, got {result}");
            });
    }

    /// <summary>
    /// Property 9: When every month is close to the median (within the deviation threshold),
    /// no outlier is flagged — avoids highlighting normal month-to-month variation.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "9")]
    public Property FindOutlierIndex_ReturnsNull_WhenNoMonthDeviatesEnough()
    {
        return Prop.ForAll(
            Gen.Choose(900, 1100).ToArbitrary(),
            variation =>
            {
                var amounts = new List<decimal> { 1000m, 1000m, 1000m, 1000m, 1000m, variation };
                int? result = FindOutlierIndex(amounts, Median(amounts));

                return (result == null)
                    .Label($"Expected no outlier, got index {result}");
            });
    }

    /// <summary>
    /// Property 10: A month with zero transactions of any type (e.g. before the user started
    /// using the app) is excluded from the median entirely — it does NOT count as a real zero.
    /// This is what lets a brand-new user's median reflect only the months they've actually
    /// used the app, instead of being dragged toward zero by unused history.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "10")]
    public Property MonthsWithNoTransactionsAtAll_AreExcluded_FromTheMedian()
    {
        var testInput =
            from amounts in Gen.ListOf(Gen.Choose(1, 100_000), HistoryMonths - 1)
                .Select(list => list.Select(a => a / 100m).ToList())
            from emptyMonthPosition in Gen.Choose(0, HistoryMonths - 1)
            select (amounts, emptyMonthPosition);

        return Prop.ForAll(
            testInput.ToArbitrary(),
            input =>
            {
                var (amounts, emptyMonthPosition) = input;
                var transactions = BuildMonthlyIncomeTransactionsSkipping(amounts, emptyMonthPosition);

                var result = ComputeAveragesProjections(transactions);

                decimal expectedMedian = Median(amounts); // median of only the 5 real months, ignoring the empty one

                return (result.AverageIncome == expectedMedian)
                    .Label($"AverageIncome should ignore the empty month and be {expectedMedian}, was {result.AverageIncome}")
                    .And(result.RealMonthsCount == HistoryMonths - 1)
                    .Label($"RealMonthsCount should be {HistoryMonths - 1}, was {result.RealMonthsCount}");
            });
    }

    /// <summary>
    /// Builds one Income transaction for each of the 6 months in the window, EXCEPT
    /// <paramref name="emptyMonthPosition"/>, which gets no transaction at all — simulating a
    /// month before the user started using the app (as opposed to a month with a real R$0 total).
    /// </summary>
    private static List<TransactionDTO> BuildMonthlyIncomeTransactionsSkipping(List<decimal> amounts, int emptyMonthPosition)
    {
        var from = new DateTime(Today.Year, Today.Month, 1).AddMonths(-(HistoryMonths - 1));
        var transactions = new List<TransactionDTO>();
        int amountCursor = 0;

        for (int i = 0; i < HistoryMonths; i++)
        {
            if (i == emptyMonthPosition) continue;

            var month = from.AddMonths(i);
            transactions.Add(new TransactionDTO
            {
                TransactionId = Guid.NewGuid(),
                UserId = 1,
                Description = "Income",
                Date = new DateTime(month.Year, month.Month, 15, 12, 0, 0, DateTimeKind.Utc),
                Amount = amounts[amountCursor++],
                Type = TransactionType.Income,
                CategoryId = 1,
                Repetition = Repetition.None,
                AccountId = 1,
                SyncStatus = TransactionSyncStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        return transactions;
    }
}
