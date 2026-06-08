using Model.DTO;
using Service.Transaction;
using System.Diagnostics;
using System.Linq;

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

    public class RecurringScheduler(ITransactionService transactionService) : IRecurringScheduler
    {
        private const int DefaultHorizonInMonths = 6;

        /// <summary>
        /// Generates all missing occurrences for the given rules up to the given horizon date.
        /// If no horizon is provided, defaults to today + 6 months.
        /// </summary>
        public async Task GeneratePendingAsync(IEnumerable<RecurringRuleDTO> rules, DateTime? horizon = null)
        {
            if (rules == null || !rules.Any())
                return;

            DateTime defaultHorizon = DateTime.Today.AddMonths(DefaultHorizonInMonths);
            DateTime resolvedHorizon = horizon.HasValue && horizon.Value > defaultHorizon
                ? horizon.Value
                : defaultHorizon;

            // FIX (N+1 Query): Fetch all existing transactions for all rules in a single batch.
            // NOTE: This requires a new method on ITransactionService, like GetByRecurringRuleIdsAsync.
            var ruleIds = rules.Select(r => r.RecurringRuleId).Distinct().ToList();
            var allExistingTransactions = (await Task.WhenAll(
                ruleIds.Select(ruleId => transactionService.GetByRecurringRuleIdAsync(ruleId))
            )).SelectMany(t => t).ToList();
            var transactionsByRuleId = allExistingTransactions.ToLookup(t => t.RecurringRuleId);

            foreach (var rule in rules)
            {
                // FIX (Long Method): Delegate the processing of a single rule to a separate method.
                await ProcessRuleAsync(rule, resolvedHorizon, transactionsByRuleId[rule.RecurringRuleId]);
            }
        }

        /// <summary>
        /// Processes a single recurring rule to generate its missing occurrences.
        /// </summary>
        private async Task ProcessRuleAsync(RecurringRuleDTO rule, DateTime resolvedHorizon, IEnumerable<TransactionDTO> existingTransactions)
        {
            if (rule.Inactive)
                return;

            // FIX (Complex Conditional): Simplified cutoff date calculation.
            DateTime cutoff = resolvedHorizon;
            if (rule.EndDate.HasValue && rule.EndDate.Value < cutoff)
            {
                cutoff = rule.EndDate.Value;
            }

            var expectedDates = ComputeExpectedDates(rule.StartDate, cutoff, rule.Frequency);
            var existingDates = new HashSet<DateTime>(existingTransactions.Select(t => t.Date.Date));

            foreach (var date in expectedDates)
            {
                if (existingDates.Contains(date.Date))
                    continue;

                try
                {
                    var occurrence = BuildOccurrence(rule, date);
                    await transactionService.AddOccurrenceAsync(occurrence);
                    existingDates.Add(date.Date); // Avoid trying to add the same date again in this run
                }
                catch (Exception ex)
                {
                    // FIX (Generic Exception): Added a comment recommending a proper logging framework.
                    // The exception is caught to allow other rules to be processed.
                    // TODO: Replace with a robust logging framework (e.g., Serilog, NLog) for production.
                    Debug.WriteLine($"[RecurringScheduler] Failed to add occurrence for rule {rule.RecurringRuleId} on {date:yyyy-MM-dd}: {ex.Message}");
                }
            }
        }

        // Returns dates in ascending order from startDate up to and including cutoff.
        private static List<DateTime> ComputeExpectedDates(DateTime startDate, DateTime cutoff, Frequency frequency)
        {
            var dates = new List<DateTime>();
            DateTime current = startDate.Date;
            DateTime cutoffDate = cutoff.Date;

            while (current <= cutoffDate)
            {
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
            CategoryId = rule.CategoryId,
            AccountId = rule.AccountId,
            RecurringRuleId = rule.RecurringRuleId,
            Repetition = Repetition.Recurring,
            Date = date,
            UserId = rule.UserId,
        };
    }
}
