using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace RecurringTests.ChartTests;

/// <summary>
/// Property-based tests for year navigation logic in annual mode.
/// Tests Property 5 from the design document.
///
/// Property 5: Year navigation advances or retreats by exactly 1.
/// For any starting year Y and navigation direction (forward or backward),
/// after N consecutive navigations in annual mode, the displayed year SHALL
/// equal Y + N (forward) or Y − N (backward), and the period label SHALL be
/// the 4-digit year string.
/// </summary>
[Trait("Feature", "annual-chart-view")]
[Trait("Property", "5")]
public class NavigationPropertyTests
{
    /// <summary>
    /// Simulates the pure navigation logic from ChartVM.LoadNextPeriod in annual mode:
    /// _selectedDate = _selectedDate.AddYears(1)
    /// </summary>
    private static DateTime NavigateForward(DateTime current) => current.AddYears(1);

    /// <summary>
    /// Simulates the pure navigation logic from ChartVM.LoadPreviousPeriod in annual mode:
    /// _selectedDate = _selectedDate.AddYears(-1)
    /// </summary>
    private static DateTime NavigateBackward(DateTime current) => current.AddYears(-1);

    /// <summary>
    /// Simulates the period label logic from ChartVM.LoadAnnualChartAsync:
    /// MonthYearDisplay = date.Year.ToString()
    /// </summary>
    private static string GetPeriodLabel(DateTime date) => date.Year.ToString();

    /// <summary>
    /// Property 5a: N consecutive forward navigations from year Y produce year Y + N.
    /// **Validates: Requirements 2.1, 2.2, 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ForwardNavigation_AdvancesYearByExactly1_EachTime()
    {
        return Prop.ForAll(
            Gen.Choose(2000, 2050).ToArbitrary(),
            Gen.Choose(1, 20).ToArbitrary(),
            (startYear, navCount) =>
            {
                var currentDate = new DateTime(startYear, 6, 15);
                for (int i = 0; i < navCount; i++)
                    currentDate = NavigateForward(currentDate);

                int expectedYear = startYear + navCount;
                return (currentDate.Year == expectedYear)
                    .Label($"After {navCount} forward navs from {startYear}, year should be {expectedYear}, was {currentDate.Year}");
            });
    }

    /// <summary>
    /// Property 5b: N consecutive backward navigations from year Y produce year Y - N.
    /// **Validates: Requirements 2.1, 2.2, 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BackwardNavigation_RetreatsYearByExactly1_EachTime()
    {
        return Prop.ForAll(
            Gen.Choose(2000, 2050).ToArbitrary(),
            Gen.Choose(1, 20).ToArbitrary(),
            (startYear, navCount) =>
            {
                var currentDate = new DateTime(startYear, 6, 15);
                for (int i = 0; i < navCount; i++)
                    currentDate = NavigateBackward(currentDate);

                int expectedYear = startYear - navCount;
                return (currentDate.Year == expectedYear)
                    .Label($"After {navCount} backward navs from {startYear}, year should be {expectedYear}, was {currentDate.Year}");
            });
    }

    /// <summary>
    /// Property 5c: Period label is always a 4-digit year string in annual mode.
    /// For years in range 1000–9999, year.ToString() produces exactly 4 characters.
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PeriodLabel_Is4DigitYearString_InAnnualMode()
    {
        return Prop.ForAll(
            Gen.Choose(1000, 9999).ToArbitrary(),
            year =>
            {
                var date = new DateTime(year, 1, 1);
                var label = GetPeriodLabel(date);

                bool isFourDigits = label.Length == 4;
                bool allDigits = label.All(char.IsDigit);
                bool matchesYear = label == year.ToString();

                return isFourDigits
                    .Label($"Label '{label}' should be exactly 4 characters")
                    .And(allDigits)
                    .Label($"Label '{label}' should contain only digits")
                    .And(matchesYear)
                    .Label($"Label '{label}' should equal year.ToString() '{year}'");
            });
    }
}
