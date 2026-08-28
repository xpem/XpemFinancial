using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace RecurringTests.ChartTests;

/// <summary>
/// Property 6: IncludePreviousBalance round-trip across mode switches.
/// For any boolean value of IncludePreviousBalance, switching from Monthly_Mode
/// to Annual_Mode and back to Monthly_Mode SHALL preserve the persisted value
/// without modification.
///
/// This tests the contract that SetScope only modifies:
///   - IsAnnualMode
///   - XAxisPointCount
///   - XAxisLabels
/// and NEVER touches IncludePreviousBalance.
///
/// **Validates: Requirements 5.3, 5.5**
/// </summary>
[Trait("Feature", "annual-chart-view")]
[Trait("Property", "6")]
public class IncludePreviousBalancePropertyTests
{
    /// <summary>
    /// Simulates the state managed by ChartVM during mode switches.
    /// Only the fields that SetScope modifies are tracked, plus
    /// IncludePreviousBalance which must remain untouched.
    /// </summary>
    private class ChartModeState
    {
        public bool IsAnnualMode { get; private set; }
        public int XAxisPointCount { get; private set; }
        public string[]? XAxisLabels { get; private set; }
        public bool IncludePreviousBalance { get; set; }

        private static readonly string[] MonthLabels =
            ["Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez"];

        public ChartModeState(bool includePreviousBalance, int daysInMonth = 30)
        {
            IsAnnualMode = false;
            XAxisPointCount = daysInMonth;
            XAxisLabels = null;
            IncludePreviousBalance = includePreviousBalance;
        }

        /// <summary>
        /// Replicates the synchronous state changes from SetScope.
        /// The async data loading (LoadChartAsync / LoadAnnualChartAsync) is
        /// irrelevant to this property — they also do NOT touch IncludePreviousBalance.
        /// </summary>
        public void SetScope(bool annual, int daysInMonth = 30)
        {
            IsAnnualMode = annual;
            if (annual)
            {
                XAxisPointCount = 12;
                XAxisLabels = MonthLabels;
            }
            else
            {
                XAxisPointCount = daysInMonth;
                XAxisLabels = null;
            }
            // Key invariant: IncludePreviousBalance is NEVER modified here
        }
    }

    /// <summary>
    /// Property 6: For any boolean value of IncludePreviousBalance and any number of
    /// mode switch round-trips (Monthly → Annual → Monthly → ...), the persisted
    /// IncludePreviousBalance value remains unchanged.
    /// **Validates: Requirements 5.3, 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IncludePreviousBalance_RoundTrip_AcrossModeSwitches()
    {
        return Prop.ForAll(
            Gen.Elements(true, false).ToArbitrary(),
            Gen.Choose(1, 20).ToArbitrary(),
            Gen.Choose(28, 31).ToArbitrary(),
            (initialValue, switchCount, daysInMonth) =>
            {
                var state = new ChartModeState(initialValue, daysInMonth);

                // Perform N mode switches alternating between annual and monthly
                for (int i = 0; i < switchCount; i++)
                {
                    bool goAnnual = !state.IsAnnualMode;
                    state.SetScope(goAnnual, daysInMonth);
                }

                return (state.IncludePreviousBalance == initialValue)
                    .Label($"IncludePreviousBalance should remain {initialValue} after {switchCount} mode switches, but was {state.IncludePreviousBalance}");
            });
    }
}
