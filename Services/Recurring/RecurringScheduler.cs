using Model.DTO;
using Repo;
using Service.Transaction;
using System.Diagnostics;

namespace Service.Recurring
{
    public interface IRecurringScheduler
    {
        /// <summary>
        /// Generates all missing occurrences for the given rules up to the given horizon date.
        /// If no horizon is provided, defaults to today + 6 months.
        /// </summary>
        Task GeneratePendingAsync(IEnumerable<RecurringRuleDTO> rules, DateTime? horizon = null);
    }

    public class RecurringScheduler(ITransactionService transactionService, ITransactionRepo transactionRepo) : IRecurringScheduler
    {
        private const int DefaultHorizonInMonths = 6;

        // How far into the past we look for missing occurrences.
        // Gaps older than this are assumed to be intentional deletions and are not regenerated.
        private const int LookbackMonths = 1;

        public async Task GeneratePendingAsync(IEnumerable<RecurringRuleDTO> rules, DateTime? horizon = null)
        {
            if (rules == null || !rules.Any())
                return;

            DateTime defaultHorizon = DateTime.Today.AddMonths(DefaultHorizonInMonths);
            DateTime resolvedHorizon = horizon.HasValue && horizon.Value > defaultHorizon
                ? horizon.Value
                : defaultHorizon;

            // Fix #1: sequential fetches instead of Task.WhenAll to avoid concurrent SQLite
            // readers racing on the same connection pool under write load.
            foreach (var rule in rules)
            {
                var existingTransactions = await transactionService.GetByRecurringRuleIdAsync(rule.RecurringRuleId);
                await ProcessRuleAsync(rule, resolvedHorizon, existingTransactions);
            }
        }

        private async Task ProcessRuleAsync(
            RecurringRuleDTO rule,
            DateTime resolvedHorizon,
            IEnumerable<TransactionDTO> existingTransactions)
        {
            if (rule.Inactive)
                return;

            DateTime cutoff = resolvedHorizon;
            if (rule.EndDate.HasValue && rule.EndDate.Value < cutoff)
                cutoff = rule.EndDate.Value;

            // Fix #3: don't walk the entire history from StartDate.
            // Start from max(StartDate, today - LookbackMonths) so ancient gaps that were
            // intentionally deleted are never regenerated, and the loop stays short.
            DateTime lookbackStart = DateTime.Today.AddMonths(-LookbackMonths);
            DateTime effectiveStart = rule.StartDate.Date > lookbackStart
                ? rule.StartDate.Date
                : lookbackStart;

            // Fix #2: exclude inactive (soft-deleted) occurrences from the seen-dates set so
            // that a date whose occurrence was deleted is not permanently skipped.
            var existingDates = new HashSet<DateTime>(
                existingTransactions
                    .Where(t => !t.Inactive)
                    .Select(t => t.Date.Date));

            var expectedDates = ComputeExpectedDates(rule.StartDate, effectiveStart, cutoff, rule.Frequency);

            foreach (var date in expectedDates)
            {
                if (existingDates.Contains(date.Date))
                    continue;

                try
                {
                    var occurrence = BuildOccurrence(rule, date);

                    // Req 6.4: Skip generation if deterministic Guid derivation returned Guid.Empty
                    // (indicates invalid RecurringRuleId or default DateTime)
                    if (occurrence.TransactionId == Guid.Empty)
                    {
                        Debug.WriteLine(
                            $"[RecurringScheduler] Skipping occurrence for rule " +
                            $"{rule.RecurringRuleId} on {date:yyyy-MM-dd}: invalid inputs for deterministic Guid.");
                        continue;
                    }

                    // Req 6.3: Deduplication by TransactionId — if a record with this
                    // deterministic Guid already exists, skip generation
                    var existing = await transactionRepo.GetByTransactionIdAsync(occurrence.TransactionId);
                    if (existing is not null)
                    {
                        existingDates.Add(date.Date);
                        continue;
                    }

                    await transactionService.AddOccurrenceAsync(occurrence);
                    existingDates.Add(date.Date);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(
                        $"[RecurringScheduler] Failed to add occurrence for rule " +
                        $"{rule.RecurringRuleId} on {date:yyyy-MM-dd}: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Returns all dates produced by stepping from <paramref name="ruleStart"/> that fall
        /// within [<paramref name="windowStart"/>, <paramref name="cutoff"/>], in ascending order.
        ///
        /// We still need <paramref name="ruleStart"/> to compute the correct phase of the
        /// recurrence (e.g. a monthly rule starting on the 15th always lands on the 15th).
        /// </summary>
        private static List<DateTime> ComputeExpectedDates(
            DateTime ruleStart,
            DateTime windowStart,
            DateTime cutoff,
            Frequency frequency)
        {
            var dates = new List<DateTime>();
            DateTime current = ruleStart.Date;
            DateTime cutoffDate = cutoff.Date;
            DateTime windowStartDate = windowStart.Date;

            // Advance from ruleStart until we reach the window, then collect.
            while (current <= cutoffDate)
            {
                if (current >= windowStartDate)
                    dates.Add(current);

                current = Step(current, frequency);
            }

            return dates;
        }

        private static DateTime Step(DateTime date, Frequency frequency) => frequency switch
        {
            Frequency.Monthly => date.AddMonths(1),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unknown frequency value.")
        };

        private static TransactionDTO BuildOccurrence(RecurringRuleDTO rule, DateTime date) => new()
        {
            Description = rule.Description ?? string.Empty,
            Amount = rule.Amount,
            Type = rule.Type,
            // CategoryId = 0 is the sentinel for "no category" in RecurringRuleDTO (int, non-nullable).
            // TransactionDTO.CategoryId is int? — translate 0 → null so we never write an invalid FK.
            CategoryId = rule.CategoryId == 0 ? null : rule.CategoryId,
            AccountId = rule.AccountId,
            RecurringRuleId = rule.RecurringRuleId,
            Repetition = Repetition.Recurring,
            Date = date,
            UserId = rule.UserId,
            TransactionId = DeterministicGuid.FromRecurringRule(rule.RecurringRuleId, date),
        };
    }
}