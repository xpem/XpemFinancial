using Model.DTO;
using Service.Transaction;
using System.Diagnostics;

namespace Service.Recurring
{
    public interface IRecurringScheduler
    {
        /// <summary>
        /// Generates all missing occurrences for the given rules up to the given horizon date.
        /// If no horizon is provided, defaults to today + 3 months.
        /// </summary>
        Task GeneratePendingAsync(IEnumerable<RecurringRuleDTO> rules, DateTime? horizon = null);
    }

    public class RecurringScheduler(ITransactionService transactionService) : IRecurringScheduler
    {
        public async Task GeneratePendingAsync(IEnumerable<RecurringRuleDTO> rules, DateTime? horizon = null)
        {
            DateTime defaultHorizon = DateTime.Today.AddMonths(3);
            DateTime resolvedHorizon = horizon.HasValue && horizon.Value > defaultHorizon
                ? horizon.Value
                : defaultHorizon;

            foreach (var rule in rules)
            {
                // Skip inactive rules (Requirement 5.4)
                if (rule.Inactive)
                    continue;

                DateTime cutoff = rule.EndDate.HasValue
                    ? (rule.EndDate.Value < resolvedHorizon ? rule.EndDate.Value : resolvedHorizon)
                    : resolvedHorizon;

                // Compute all expected occurrence dates in ascending order
                var expectedDates = ComputeExpectedDates(rule.StartDate, cutoff, rule.Frequency);

                // Fetch existing occurrences for this rule
                var existing = await transactionService.GetByRecurringRuleIdAsync(rule.RecurringRuleId);

                // Dates already covered by a projection OR a customized exception — skip both.
                // Customized occurrences are user-edited exceptions that must never be overwritten
                // by a freshly generated projection.
                var existingDates = new HashSet<DateTime>(
                    existing.Select(t => t.Date.Date)
                );

                // Generate missing occurrences in ascending order.
                // Dates already in existingDates are skipped — this covers both regular projections
                // and customized exceptions pulled from the server, so neither is ever overwritten.
                foreach (var date in expectedDates)
                {
                    if (existingDates.Contains(date.Date))
                        continue;

                    try
                    {
                        var occurrence = BuildOccurrence(rule, date);
                        await transactionService.AddOccurrenceAsync(occurrence);
                        existingDates.Add(date.Date); // keep the set current within this run
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[RecurringScheduler] Failed to add occurrence for rule {rule.RecurringRuleId} on {date:yyyy-MM-dd}: {ex.Message}");
                    }
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
            //Frequency.Daily   => date.AddDays(1),
            //Frequency.Weekly  => date.AddDays(7),
            Frequency.Monthly => date.AddMonths(1),
            //Frequency.Yearly  => date.AddYears(1),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unknown frequency value.")
        };

        //private static Repetition MapFrequencyToRepetition(Frequency frequency) => frequency switch
        //{
        //    //Frequency.Daily   => Repetition.Daily,
        //    //Frequency.Weekly  => Repetition.Weekly,
        //    Frequency.Monthly => Repetition.Monthly,
        //    //Frequency.Yearly  => Repetition.Yearly,
        //    _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unknown frequency value.")
        //};

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
            // CreatedAt and UpdatedAt are set by AddOccurrenceAsync
        };
    }
}
